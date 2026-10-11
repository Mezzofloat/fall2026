using System;
using System.IO;
using System.Text;

namespace Breathe
{
    public static class LiveRules
    {
        public static bool CanAnswer(Act act) => act==Act.Name||act==Act.Unreadable||act==Act.Challenge||act==Act.Team||act==Act.Gap||act==Act.GapChoice||act==Act.Offer;
        /// <summary>Mr. Vale can be talked to wherever he exists (everything except the menus and the ending).</summary>
        public static bool CanTalk(Act act)=>act!=Act.Welcome&&act!=Act.Resume&&act!=Act.Ending;
        public static bool Allowed(Act act,string intent)
        {
            if(intent=="chat") return CanTalk(act);   // free chat never changes the story
            if(!CanAnswer(act)) return false;
            if(intent=="stop"||intent=="pause"||intent=="clarify") return true;
            if(act==Act.GapChoice) return intent=="keep_memory"||intent=="redact_memory";
            if(act==Act.Offer) return intent=="set_boundary"||intent=="unlimited";
            return intent=="answer";
        }
        public static bool Speakable(string text)
        {
            if(string.IsNullOrWhiteSpace(text)||text.Length>180||text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).Length>32) return false;
            foreach(char c in text) if(char.IsControl(c)||c=='<'||c=='>'||c=='`'||c=='{'||c=='}') return false;
            return !text.Contains("://");
        }
        public static byte[] EncodeWav(float[] samples,int rate)
        {
            using(var output=new MemoryStream()) using(var writer=new BinaryWriter(output))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                foreach(var sample in samples) writer.Write((short)(Math.Max(-1,Math.Min(1,float.IsNaN(sample)?0:sample))*32767));
                return output.ToArray();
            }
        }
        public static float[] DecodeWav(byte[] bytes,out int rate)
        {
            rate=0;
            if(bytes==null||bytes.Length<44||bytes.Length>4_000_000) throw new InvalidDataException("Invalid speech audio.");
            using(var reader=new BinaryReader(new MemoryStream(bytes)))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF") throw new InvalidDataException();
                reader.ReadInt32(); if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE") throw new InvalidDataException();
                bool format=false;
                while(reader.BaseStream.Position+8<=bytes.Length)
                {
                    string kind=Encoding.ASCII.GetString(reader.ReadBytes(4)); int size=reader.ReadInt32();
                    if(size<0||size>bytes.Length-reader.BaseStream.Position) throw new InvalidDataException();
                    long end=reader.BaseStream.Position+size;
                    if(kind=="fmt ")
                    {
                        if(size<16||reader.ReadInt16()!=1||reader.ReadInt16()!=1) throw new InvalidDataException();
                        rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();
                        if(reader.ReadInt16()!=16||rate<8000||rate>48000) throw new InvalidDataException(); format=true;
                    }
                    else if(kind=="data")
                    {
                        if(!format||size%2!=0||size==0) throw new InvalidDataException();
                        var samples=new float[size/2];for(int i=0;i<samples.Length;i++) samples[i]=reader.ReadInt16()/32768f;
                        return samples;
                    }
                    reader.BaseStream.Position=end+(size%2);
                }
            }
            throw new InvalidDataException();
        }
    }
}
