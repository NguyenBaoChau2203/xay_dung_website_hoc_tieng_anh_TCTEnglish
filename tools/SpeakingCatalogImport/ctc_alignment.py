"""Forced alignment of supplied captions only; never decode/transcribe new words."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import unicodedata
import wave

MODEL_SHA = '488fd4f16de84438ffc945334278c1b9fb9b7159a806c1080b16111a958c945d'
MODEL_NAME = 'wav2vec2_fairseq_base_ls960_asr_ls960.pth'
IMAGE = 'sha256:a4fa1c8004712be22ae6290720c412722fc1f6876bcb7d142a762a73d90796b3'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def atomic(file, value):
    file = Path(file)
    file.with_suffix(file.suffix + '.tmp').write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
    os.replace(file.with_suffix(file.suffix + '.tmp'), file)

def normalize_supplied_text(text):
    # Ambiguous large numbers/dates are flagged for review, never silently removed.
    small = ['zero','one','two','three','four','five','six','seven','eight','nine','ten','eleven','twelve','thirteen','fourteen','fifteen','sixteen','seventeen','eighteen','nineteen']
    tens = ['','','twenty','thirty','forty','fifty','sixty','seventy','eighty','ninety']
    def cardinal(value):
        if value < 20:return small[value]
        if value < 100:return tens[value//10]+(' '+small[value%10] if value%10 else '')
        if value < 1000:return small[value//100]+' hundred'+(' '+cardinal(value%100) if value%100 else '')
        if value < 1000000:return cardinal(value//1000)+' thousand'+(' '+cardinal(value%1000) if value%1000 else '')
        raise ValueError('numeric_reading_exceeds_bound')
    def number(match):
        literal=match[0];value=int(literal.replace(',',''))
        if len(literal)>1 and literal.startswith('0'):
            raise ValueError('ambiguous_numeric_reading_requires_review')
        if 1900<=value<=2099 and ',' not in literal and not re.search(r'dollars?|pounds?|euros?',text,re.I):
            if value == 2000:return ' two thousand '
            if 2000<value<2010:return ' two thousand '+small[value%100]+' '
            return ' '+cardinal(value//100)+(' '+cardinal(value%100) if value%100 else ' hundred')+' '
        return ' '+cardinal(value)+' '
    if re.fullmatch(r'\s*\[[^\]]+\]\s*', text):
        raise ValueError('non_speech_caption_requires_classification')
    text = unicodedata.normalize('NFKD', text.replace('’', "'"))
    def clock(match):
        hour,minute=int(match[1]),int(match[2])
        if not 1<=hour<=23 or not 0<=minute<=59:raise ValueError('invalid_clock_reading')
        return ' '+cardinal(hour)+(' '+('oh '+small[minute] if minute<10 else cardinal(minute)) if minute else '')+' '
    text=re.sub(r'\b(\d{1,2}):(\d{2})\b',clock,text)
    if any(ord(c)>127 and c.isalpha() for c in text) or re.search(r'\d:\d|-\d|\d(?:st|nd|rd|th)\b|[$£€]\s*[\d,]+\.\d', text, re.I):
        raise ValueError('ambiguous_script_or_numeric_reading_requires_review')
    text=re.sub(r'([$£€])\s*(\d{1,3}(?:,\d{3})+|\d+)',lambda m:' '+cardinal(int(m[2].replace(',','')))+' '+{'$':'dollars','£':'pounds','€':'euros'}[m[1]]+' ',text)
    text=re.sub(r'\d+\.\d+',lambda m:' '+cardinal(int(m[0].split('.')[0]))+' point '+' '.join(small[int(d)] for d in m[0].split('.')[1])+' ',text)
    text=text.replace('%',' percent ')
    text = text.encode('ascii', 'ignore').decode().upper()
    text = re.sub(r'\d{1,3}(?:,\d{3})+|\d+', number, text).upper()
    words = re.findall(r"[A-Z]+(?:'[A-Z]+)*", text)
    if not words:
        raise ValueError('no_supplied_english_tokens')
    return words

def align_path(emission, target):
    """CTC Viterbi, with wildcard context only before/after the supplied text."""
    import torch
    if emission.ndim != 2 or not len(target) or len(target) * 2 + 1 > 12001 or len(emission) > 16000:
        raise ValueError('alignment_bounds')
    states = torch.zeros(2 * len(target) + 1, dtype=torch.long)
    states[1::2] = target.long()
    count = len(states)
    dp = torch.full((count,), -torch.inf)
    dp[0], dp[1] = emission[0].max(), emission[0, states[1]]
    back = torch.zeros((len(emission), count), dtype=torch.uint8)
    skip = (states != 0) & (states != torch.roll(states, 2))
    skip[:2] = False
    for t in range(1, len(emission)):
        step = torch.roll(dp, 1); step[0] = -torch.inf
        jump = torch.roll(dp, 2); jump[:2] = -torch.inf; jump[~skip] = -torch.inf
        values, choices = torch.stack([dp, step, jump]).max(dim=0)
        row = emission[t, states].clone()
        # Untranscribed padding may contain neighboring speech. It is not a blank
        # or a new transcript, and remains outside whole-speech quality evidence.
        row[0] = row[-1] = emission[t].max()
        dp = values + row
        back[t] = choices
    state = count - 1 if dp[-1] > dp[-2] else count - 2
    if not torch.isfinite(dp[state]):
        raise ValueError('no_complete_ctc_path')
    path = torch.zeros(len(emission), dtype=torch.long)
    for t in range(len(emission) - 1, -1, -1):
        path[t] = state
        state -= int(back[t, state])
    spans = []
    for i, token in enumerate(target):
        frames = (path == 2 * i + 1).nonzero().flatten()
        if not len(frames):
            raise ValueError('unaligned_supplied_token')
        spans.append((int(frames[0]), int(frames[-1]) + 1, float(emission[frames, token.long()].exp().mean())))
    return spans

def emissions_for_pcm(model, samples):
    """Bound neural attention to 9.5-second inputs; retain measured frame offsets."""
    import torch
    core, context, stride, receptive_center = 120000, 16000, 320, 200
    values, times = [], []
    for start in range(0, len(samples), core):
        end = min(start + core, len(samples))
        left, right = max(0, start - context), min(len(samples), end + context)
        with torch.inference_mode():
            result, _ = model(torch.from_numpy(samples[left:right]).unsqueeze(0))
            result = result[0].log_softmax(-1).cpu()
        centers = left + torch.arange(len(result)) * stride + receptive_center
        owned = (centers >= start) & (centers < end)
        values.append(result[owned])
        times.extend((centers[owned].double() / 16000).tolist())
    if not times or any(b <= a for a, b in zip(times, times[1:])):
        raise ValueError('invalid_measured_frame_offsets')
    return torch.cat(values), times

def align_window(model, labels, samples, window, emission_cache=None):
    import torch
    dictionary = {c:i for i,c in enumerate(labels)}
    members = window.get('members') or [dict(window, owned=True)]
    members = [m for m in members if not re.fullmatch(r'\s*\[(?:music|applause|laughter)\]\s*',m['text'],re.I)]
    if not members:return []
    supplied = [normalize_supplied_text(m['text']) for m in members]
    text = '|'.join(word for sentence in supplied for word in sentence)
    if emission_cache is not None and emission_cache.exists():
        cached=torch.load(emission_cache,map_location='cpu',weights_only=True)
        emission,times=cached['emission'],cached['times'].tolist()
        if emission.ndim!=2 or len(emission)!=len(times) or emission.shape[1]!=len(labels) or not torch.isfinite(emission).all() or any(b<=a for a,b in zip(times,times[1:])):
            raise ValueError('acoustic_cache_conflict')
    else:
        emission, times = emissions_for_pcm(model, samples)
        if emission_cache is not None:
            emission_cache.parent.mkdir(exist_ok=True)
            torch.save({'emission':emission,'times':torch.tensor(times,dtype=torch.float64)},str(emission_cache)+'.tmp')
            os.replace(str(emission_cache)+'.tmp',emission_cache)
    target = torch.tensor([dictionary[c] for c in text], dtype=torch.long)
    spans = align_path(emission, target)
    words, chars = [], []
    for c, (start, end, confidence) in zip(text, spans):
        if c == '|':
            if chars:
                words.append(chars); chars = []
        else:
            chars.append((c, window['start'] + max(0, times[start] - .01), window['start'] + min(len(samples)/16000, times[end-1] + .01), confidence))
    if chars:
        words.append(chars)
    cues, cursor = [], 0
    for member, normalized in zip(members, supplied):
        selected = words[cursor:cursor+len(normalized)]; cursor += len(normalized)
        if len(selected) != len(normalized):
            raise ValueError('supplied_word_mapping_incomplete')
        if member.get('owned', True):
            cues.append({'sourceIndex':member['index'], 'text':member['text'], 'startTime':selected[0][0][1], 'endTime':selected[-1][-1][2],
                         'alignmentConfidence':sum(c[3] for w in selected for c in w)/sum(len(w) for w in selected)})
    if cursor != len(words):
        raise ValueError('unexpected_aligned_word_count')
    return cues

def main():
    import numpy as np
    import torch, torchaudio
    parser = argparse.ArgumentParser(); parser.add_argument('job'); args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9_-]{11}(?:-g10c[12]|-g10)?', args.job):
        raise ValueError('invalid_selected_job')
    root = Path('/data'); folder = root/'alignment'/args.job
    source = json.loads((folder/'input.json').read_text())
    if torch.__version__ != '2.8.0' or torchaudio.__version__ != '2.8.0':
        raise ValueError('unverified_alignment_runtime')
    weights = root/'torch-cache/hub/checkpoints'/MODEL_NAME
    if sha(weights.read_bytes()) != MODEL_SHA:
        raise ValueError('unverified_acoustic_weights')
    torch.set_num_threads(2)
    model = torchaudio.pipelines.WAV2VEC2_ASR_BASE_960H.get_model().eval()
    labels = torchaudio.pipelines.WAV2VEC2_ASR_BASE_960H.get_labels()
    output = folder/'ctc'; output.mkdir(exist_ok=True)
    algorithm_sha = sha(Path(__file__).read_bytes())
    cues, flags = [], []
    for window in source['windows']:
        wav = folder/'corpus'/f"{window['index']:05}.wav"
        fingerprint = sha(wav.read_bytes()+json.dumps(window,sort_keys=True).encode()+MODEL_SHA.encode()+algorithm_sha.encode())
        receipt = output/(fingerprint+'.json')
        if receipt.exists():
            result = json.loads(receipt.read_text())
        else:
            try:
                with wave.open(str(wav),'rb') as stream:
                    if stream.getframerate()!=16000 or stream.getnchannels()!=1 or stream.getsampwidth()!=2:
                        raise ValueError('invalid_pcm_format')
                    samples = np.frombuffer(stream.readframes(stream.getnframes()),dtype='<i2').astype('float32')/32768
                feature_cache=output/'features'/(sha(wav.read_bytes()+MODEL_SHA.encode()+b'measured-frame-extraction-v1')+'.pt')
                result = {'cues':align_window(model,labels,samples,window,feature_cache), 'flags':[]}
            except ValueError as error:
                result = {'cues':[], 'flags':[f"window_{window['index']}_{error}"]}
            atomic(receipt,result)
        cues.extend(result['cues']); flags.extend(result['flags'])
        print('CTC-CHECKPOINT',source['youtubeId'],window['index'],flush=True)
    for i,cue in enumerate(cues):
        if cue['alignmentConfidence'] < .9: flags.append(f"low_alignment_confidence_{cue['sourceIndex']}")
        if i and cue['startTime'] < cues[i-1]['endTime']-.001: flags.append(f"overlap_{cue['sourceIndex']}")
    expected = 0
    for window in source['windows']:
        for member in window.get('members') or [dict(window,owned=True)]:
            if not member.get('owned',True):continue
            if re.fullmatch(r'\s*\[(?:music|applause|laughter)\]\s*',member['text'],re.I):flags.append('source_non_speech_classification_review_'+str(member['index']))
            else:expected+=1
    if len(cues)!=expected or len({c['sourceIndex'] for c in cues})!=expected: flags.append('source_cue_mapping_incomplete')
    flags += ['whole_speech_interval_review_pending','manual_token_accuracy_pending','beginning_middle_late_boundary_playback_pending']
    candidate={'youtubeId':source['youtubeId'],'sourceSha256':sha((root/'captions'/(source['youtubeId']+'.json')).read_bytes()),'mediaSha256':source['mediaSha256'],
               'measuredDurationSeconds':source['measuredDurationSeconds'],'cues':cues,'unresolvedFlags':flags,'qualityPassed':False,
               'runtime':{'image':IMAGE,'torch':torch.__version__,'torchaudio':torchaudio.__version__,'modelSha256':MODEL_SHA,'algorithmSha256':algorithm_sha,'mode':'supplied-caption forced alignment; no transcript decoding'}}
    atomic(folder/'ctc-candidate.json',candidate)
    print('CTC-CANDIDATE',source['youtubeId'],'cues='+str(len(cues)),'flags='+str(len(flags)),'quality=false',flush=True)

if __name__=='__main__':main()
