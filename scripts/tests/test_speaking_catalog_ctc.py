import importlib.util
from pathlib import Path
import unittest

module_path=Path(__file__).resolve().parents[2]/'tools/SpeakingCatalogImport/ctc_alignment.py'
spec=importlib.util.spec_from_file_location('catalog_ctc',module_path)
ctc=importlib.util.module_from_spec(spec);spec.loader.exec_module(ctc)

class CatalogCtcTests(unittest.TestCase):
    def test_numeric_reading_is_explicit_and_unsupported_text_does_not_vanish(self):
        self.assertEqual(ctc.normalize_supplied_text('Level A1. I called you 10 times.'),['LEVEL','A','ONE','I','CALLED','YOU','TEN','TIMES'])
        self.assertEqual(ctc.normalize_supplied_text('It costs $2,000.'),['IT','COSTS','TWO','THOUSAND','DOLLARS'])
        self.assertEqual(ctc.normalize_supplied_text('500 dollars and 38 degrees.'),['FIVE','HUNDRED','DOLLARS','AND','THIRTY','EIGHT','DEGREES'])
        self.assertEqual(ctc.normalize_supplied_text('Listen again: 38.5 degrees.'),['LISTEN','AGAIN','THIRTY','EIGHT','POINT','FIVE','DEGREES'])
        self.assertEqual(ctc.normalize_supplied_text('In 1972 and 2025.'),['IN','NINETEEN','SEVENTY','TWO','AND','TWENTY','TWENTY','FIVE'])
        self.assertEqual(ctc.normalize_supplied_text('It is 2:00. At 7:30.'),['IT','IS','TWO','AT','SEVEN','THIRTY'])
        for text in ['Phone 019','At 99:99','[Music]','>>>']:
            with self.assertRaises(ValueError):ctc.normalize_supplied_text(text)

    @unittest.skipUnless(importlib.util.find_spec('torch'), 'Run the pinned alignment-only Docker runtime')
    def test_ctc_skips_neighbor_context_and_preserves_actual_repeated_target_tokens(self):
        import torch
        # 0 blank,1 A,2 separator,3 B,4 untranscribed context C.
        probabilities=torch.full((9,5),.001)
        for t,label in enumerate([4,4,1,0,2,3,0,4,4]):probabilities[t,label]=.996
        spans=ctc.align_path(probabilities.log(),torch.tensor([1,2,3]))
        self.assertEqual([(s,e) for s,e,_ in spans],[(2,3),(4,5),(5,6)])
        repeat=torch.full((5,2),.001)
        for t,label in enumerate([0,1,0,1,0]):repeat[t,label]=.999
        spans=ctc.align_path(repeat.log(),torch.tensor([1,1]))
        self.assertEqual([(s,e) for s,e,_ in spans],[(1,2),(3,4)])

    @unittest.skipUnless(importlib.util.find_spec('torch'), 'Run the pinned alignment-only Docker runtime')
    def test_neural_chunk_context_retains_monotonic_measured_twenty_millisecond_frame_offsets(self):
        import torch
        import numpy as np
        class SyntheticAcousticModel:
            def __call__(self, waveform):
                frames=(waveform.shape[-1]-400)//320+1
                return torch.zeros((1,frames,5)),None
        emission,times=ctc.emissions_for_pcm(SyntheticAcousticModel(),np.zeros(16*16000,dtype='float32'))
        self.assertEqual(len(times),len(emission))
        self.assertAlmostEqual(times[0],.0125)
        self.assertTrue(any(t>15 for t in times))
        self.assertTrue(all(abs(b-a-.02)<1e-10 for a,b in zip(times,times[1:])))

if __name__=='__main__':unittest.main()
