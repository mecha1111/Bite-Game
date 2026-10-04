"""Inspect actual PE icon resources; compare all resolutions with nearest-scaled source."""
import argparse, io, json, struct
from pathlib import Path
from PIL import Image, ImageChops
p=argparse.ArgumentParser();p.add_argument('exe',type=Path);p.add_argument('source',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--reference',type=Path);a=p.parse_args();data=a.exe.read_bytes()
def u16(off):return struct.unpack_from('<H',data,off)[0]
def u32(off):return struct.unpack_from('<I',data,off)[0]
pe=u32(0x3c);assert data[pe:pe+4]==b'PE\0\0';machine=u16(pe+4);count=u16(pe+6);optional=pe+24;size=u16(pe+20);magic=u16(optional);directory=optional+(112 if magic==0x20b else 96)
sections=[]
for i in range(count):
 off=optional+size+40*i;sections.append((u32(off+12),max(u32(off+8),u32(off+16)),u32(off+20)))
def file_offset(rva):
 for va,length,raw in sections:
  if va<=rva<va+length:return raw+rva-va
 raise ValueError('Unmapped PE RVA')
base=file_offset(u32(directory+16));resources={}
def walk(relative,ids=()):
 off=base+relative;entries=u16(off+12)+u16(off+14)
 for i in range(entries):
  key,child=struct.unpack_from('<II',data,off+16+8*i)
  if key&0x80000000:
   name=base+(key&0x7fffffff);length=u16(name);key=data[name+2:name+2+length*2].decode("utf-16le")
  path=ids+(key,)
  if child&0x80000000:walk(child&0x7fffffff,path)
  else:
   rva,length=struct.unpack_from('<II',data,base+child);start=file_offset(rva);resources[path]=data[start:start+length]
walk(0)
groups=[(k,v) for k,v in resources.items() if k[0]==14];assert groups,'No GROUP_ICON resource'
source=Image.open(a.source).convert('RGBA');a.output.mkdir(parents=True,exist_ok=True);result=[]
for path,group in groups:
 reserved,kind,n=struct.unpack_from('<HHH',group);assert kind==1
 header=bytearray(struct.pack('<HHH',0,1,n));payload=bytearray();start=6+16*n
 for i in range(n):
  width,height,colors,reserved,planes,bits,length,identifier=struct.unpack_from('<BBBBHHIH',group,6+14*i)
  blob=next(v for k,v in resources.items() if k[0]==3 and k[1]==identifier)
  assert len(blob)==length
  header.extend(struct.pack('<BBBBHHII',width,height,colors,0,planes,bits,len(blob),start+len(payload)));payload.extend(blob)
 ico=bytes(header+payload);(a.output/('embedded-'+str(path[1])+'.ico')).write_bytes(ico)
 im=Image.open(io.BytesIO(ico));sizes=sorted(im.ico.sizes());comparisons=[]
 for dimensions in sizes:
  actual=im.ico.getimage(dimensions).convert('RGBA');expected=Image.open(a.reference/("source-"+str(dimensions[0])+".png")).convert("RGBA") if a.reference else source.resize(dimensions,Image.Resampling.NEAREST)
  matches=ImageChops.difference(actual,expected).getbbox() is None
  # RGB differences must also be checked when both alpha channels are opaque.
  matches=matches and ImageChops.difference(actual.convert('RGB'),expected.convert('RGB')).getbbox() is None
  comparisons.append({'size':dimensions,'matches_source_nearest':matches})
 largest=im.ico.getimage(max(sizes)).convert('RGBA');largest.save(a.output/('embedded-'+str(path[1])+'.png'));result.append({'resource':path,'frames':comparisons})
report={'exe':str(a.exe),'machine':hex(machine),'x64':machine==0x8664,'source_size':source.size,'icon_groups':result,'all_frames_match_source':all(f['matches_source_nearest'] for g in result for f in g['frames'])}
(a.output/'pe-icon-verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2));assert report['x64'] and report['all_frames_match_source']
