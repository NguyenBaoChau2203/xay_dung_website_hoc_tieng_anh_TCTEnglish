import fs from 'node:fs';
const file='data/speaking/reach-a1-c2-catalog.json',manifest=JSON.parse(fs.readFileSync(file));
// Reviewed creator-title categories. Broad exam videos remain mixed exam practice;
// they are not incorrectly relabelled as food/travel based on a single excerpt.
const groups={
 'Ăn uống':['CzXqkWprDjY','Qp5llcn6IHM','UlgT0cWCqe4','5BcuIWIx2E0','c-rI2L21KjA','MxEmCaIW2BQ','dCpBvKgdMqE','_UtQVkoYNgQ'],
 'Vui chơi & giải trí':['xvYJO5JpuJ4','6brq3fJtf1w','OMmgZLGvdMc','9MZYgioQ4c0','cK_ro3bbXSQ','VsIXyNEnXxw','zYBPIOlCkgo','q5ELgWfX0Q4','DLRpxaYZrKs','Q1lIQnUtbfk','IC6K_dWOUVc'],
 'Trò chuyện & tình cảm':['WUaRqlnenq4','-lrX0zBbNG8','ZlQbDRE7jBU','_JOf7RBDCx8','Lkc3hWemJz8','HdAqLUdygB8','sIF-ulGa6dU','YA-Rpu6X3yI','qCOiCASCGYI','gZluFVzpb8A'],
 'Gia đình & đời sống':['WV0LNIvCfvE','2kVKjwNDlUE','n74GjJQaJkA','debAxRi-jWo','tGvePkiUPNQ','8pzPtQwsUeg','BWfN2JIsV7g','pTwj0FJfjfA'],
 'Mua sắm & tài chính':['fFXR3CMy8jw','UEpAkOQ9-Hg','HjJeBn7XWhE','UtoNNhh8pZQ','0W8puiJv6VI','RU7TUBGpF0g','JcXs_EMYakY','AW2hQBJUu54','tPieJWEmIOM','kYMNY8YYK68'],
 'Sức khỏe':['F5QyH36MnVo','_nn_W2-GAyc','DtofL_9_AjQ','q_ilbziDXOk','7oMeyxbYKjM','_fyM7uk6bxE','eZzeZqdmG70'],
 'Học tập & công việc':['CZFy-Ymk6fs','lBHt3sd1TpI','irDiTOV9PEE','-RVdUqptaC4','vQIQ-qdGPRU','Yz1B1DKxi4s','lGzU_tl3tCo','XqhERu3I4S0'],
 'Xã hội & di chuyển':['G6x4T_u6EBg','wr0mSecqJzc','M3qtNiUBkqY'],
 'Khoa học & văn hóa':['WD7vbWoRRHE','G15J8yyNZz4','VXyeVhW2kkk','3HWTXd_UWhE','SWKAQPGsciw']
};
const seen=new Map();for(const [topic,ids]of Object.entries(groups))for(const id of ids){if(seen.has(id))throw Error('Duplicate topic assignment '+id);seen.set(id,topic);}
for(const item of manifest.items){
 const topic=seen.get(item.youtubeId)||(!item.isReach?'Luyện thi tổng hợp':null);
 if(!topic)throw Error('Missing reviewed category '+item.youtubeId+' '+item.topic);
 item.catalogTopic=topic;item.topicEvidence={source:item.watchUrl,detail:item.topic,method:item.isReach?'Reviewed creator-labelled subject/title':'Full mixed-topic exam; retained as combined practice'};
}
fs.writeFileSync(file+'.tmp',JSON.stringify(manifest,null,2)+'\n');fs.renameSync(file+'.tmp',file);
console.log(JSON.stringify(Object.fromEntries([...new Set(manifest.items.map(x=>x.catalogTopic))].map(t=>[t,manifest.items.filter(x=>x.catalogTopic===t).length])),null,2));
