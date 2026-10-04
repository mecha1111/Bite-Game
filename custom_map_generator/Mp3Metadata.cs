using System;
using System.Text;
namespace Gamejam2.CustomGenerator;
public sealed record Mp3Tags(string Title,string Artist,string Album,byte[]? Artwork);
/// <summary>Local ID3v2.2/2.3/2.4 and ID3v1 reader. No source path is stored.</summary>
public static class Mp3Metadata
{
    static int Size(byte[] b,int at,bool sync)=>sync?(b[at]&127)<<21|(b[at+1]&127)<<14|(b[at+2]&127)<<7|(b[at+3]&127):b[at]<<24|b[at+1]<<16|b[at+2]<<8|b[at+3];
    static string Text(ReadOnlySpan<byte> b)
    {
        if(b.Length<2)return "";int enc=b[0];b=b[1..];
        var encoding=enc==3?Encoding.UTF8:enc==2?Encoding.BigEndianUnicode:enc==1?(b.Length>=2&&b[0]==255?Encoding.Unicode:Encoding.BigEndianUnicode):Encoding.Latin1;
        return encoding.GetString(b).Trim('\0','\uFEFF',' ','\n','\r');
    }
    public static Mp3Tags Read(byte[] b,string fallback)
    {
        string title="",artist="",album="";byte[]? art=null;
        if(b.Length>10&&Encoding.ASCII.GetString(b,0,3)=="ID3")
        {
            int version=b[3],end=Math.Min(b.Length,10+Size(b,6,true)),at=10;
            if((b[5]&64)!=0&&version>=3&&at+4<end)at+=version==4?Size(b,at,true):4+Size(b,at,false);
            while(at+(version==2?6:10)<=end)
            {
                string id=Encoding.ASCII.GetString(b,at,version==2?3:4);
                int length=version==2?(b[at+3]<<16|b[at+4]<<8|b[at+5]):Size(b,at+4,version==4);at+=version==2?6:10;
                if(length<=0||length>end-at)break;
                var data=b.AsSpan(at,length);
                if(id is "TIT2" or "TT2")title=Text(data);
                if(id is "TPE1" or "TP1")artist=Text(data);
                if(id is "TALB" or "TAL")album=Text(data);
                if(id is "APIC" or "PIC"&&length<20_000_000)
                {
                    int p=1;
                    if(id=="PIC")p+=3;else{while(p<data.Length&&data[p]!=0)p++;p++;}
                    p++;int step=data[0] is 1 or 2?2:1;
                    while(p+step<=data.Length){bool zero=data[p]==0&&(step==1||data[p+1]==0);p+=step;if(zero)break;}
                    if(p<data.Length)art=data[p..].ToArray();
                }
                at+=length;
            }
        }
        if(b.Length>=128&&Encoding.ASCII.GetString(b,b.Length-128,3)=="TAG")
        {
            string Field(int p)=>Encoding.Latin1.GetString(b,b.Length-128+p,30).Trim('\0',' ');
            if(title.Length==0)title=Field(3);if(artist.Length==0)artist=Field(33);if(album.Length==0)album=Field(63);
        }
        return new(title.Length==0?fallback:title,artist,album,art);
    }
}
