const net=require('net'),fs=require('fs');
function request(command,params={}) {return new Promise((resolve,reject)=>{
 const socket=net.connect(7777,'127.0.0.1');let buffer=Buffer.alloc(0),welcomed=false;
 socket.setTimeout(10000,()=>socket.destroy(new Error('Runtime timeout')));
 socket.on('error',reject);
 socket.on('data',data=>{buffer=Buffer.concat([buffer,data]);
  while(buffer.length>=4&&buffer.length>=buffer.readUInt32LE(0)+4){
   const length=buffer.readUInt32LE(0),value=JSON.parse(buffer.subarray(4,4+length).toString());buffer=buffer.subarray(4+length);
   if(!welcomed){welcomed=true;socket.write(JSON.stringify({id:1,command,params}));}
   else {socket.end();resolve(value);}
  }
 });
});}
async function main(){
 if(process.argv.includes('--profiles')) {
  const environment='/root/Startup/ScreenHost/Gameplay/EnvironmentBackground';const reports=[];
  for(const song of ['song_1','song_2','song_3','song_4',...process.argv.filter(a=>a.startsWith('--custom-id=')).map(a=>a.slice('--custom-id='.length))]){
   const applied=await request('call_method',{path:environment,method:'Apply',args:[song]});
   const times=song==='song_4'?[0,12.492,36.926,62.16,69.637,92.201,104.35]:[0];
   for(const time of times){
    await request('call_method',{path:environment,method:'Present',args:[time,false,1.2]});
    await request('call_method',{path:environment,method:'Present',args:[time+1.2,false,1.2]});
    const art=await request('get_node',{path:environment+'/Art'});
    reports.push({song,time,apply:applied.type,profile:(await request('call_method',{path:environment,method:'get',args:['ActiveEnvironmentSongId']})).result,texture:art.data?.properties.texture,modulate:art.data?.properties.self_modulate});
   }
  }
  await request('call_method',{path:environment,method:'Apply',args:['song_1']});
  fs.writeFileSync('tools/windows-profile-runtime.json',JSON.stringify(reports,null,2));console.log(JSON.stringify(reports,null,2));return;
 }
 const result=await request('get_tree',{root:'/root/Startup',depth:10,include_properties:true});
 const rows=[];
 function walk(n){const p=n.properties||{};
  if(n.type==='AnimatedSprite2D'||/EnvironmentBackground\/(Art|ReadabilityShade)$/.test(n.path)||/VisionMask|AttackLayer|Canvas$|SettingsPopup$|ResultLayer$|GameOverLayer$|SkipConfirmation$|GeneratorPanel$|Rights/.test(n.path))
   rows.push({path:n.path,type:n.type,visible:p.visible,mouse_filter:p.mouse_filter,focus_mode:p.focus_mode,process_mode:p.process_mode,layer:p.layer,texture:p.texture,frames:p.sprite_frames,animation:p.animation,color:p.color});
  for(const c of n.children||[])walk(c);
 }
 walk(result.root);
 const screen=result.root.children.find(n=>n.name==='ScreenHost').children[0];
 const state=await request('call_method',{path:screen.path,method:'get',args:['RunState']});
 const indicators=[];
 for(const type of ['ship_horn','fishing_float','whale','sardine']) {
  const path=screen.path+'/InterferenceIndicators/SafeArea/'+type;
  indicators.push({type,initialized:(await request('call_method',{path,method:'get',args:['Initialized']})).result,animation:(await request('call_method',{path,method:'get',args:['SelectedAnimation']})).result});
 }
 const report={screen:screen.path,state:state.result,indicators,rows};
 fs.writeFileSync('tools/windows-shared-runtime.json',JSON.stringify(report,null,2));
 console.log(JSON.stringify(report,null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
