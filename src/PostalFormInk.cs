using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace QslPost
{
    internal static class PostalFormInk
    {
        public static RectangleF Area(float x,float y,float w,float h) { return new RectangleF(x,y,w,h); }

        public static void Rule(Graphics g,float x,float y,float u,float v,float width=.18f)
        {
            using(var pen=new Pen(Color.Black,width))g.DrawLine(pen,x,y,u,v);
        }

        public static void Frame(Graphics g,RectangleF area,float width=.24f)
        {
            using(var pen=new Pen(Color.Black,width))g.DrawRectangle(pen,area.X,area.Y,area.Width,area.Height);
        }

        public static void Field(Graphics g,string key,string cn,string value,float x,float y,float w,float h,
                                  bool chineseStyle=false,bool frame=true,bool centered=false,float labelHeight=0)
        {
            if(frame)Frame(g,Area(x,y,w,h),.15f);
            var label=PostalType.Label(key,cn);
            float cap=labelHeight>0?labelHeight:Math.Min(chineseStyle?7:8,h*.51f);
            PostalType.Caption(g,label,Area(x+.5f,y+.22f,w-1,cap),w<31?5.4f:6.7f,centered,chineseStyle);
            if(value.Length>0)
            {
                var fill=Area(x+.65f,y+cap+.25f,w-1.3f,h-cap-.50f);
                bool prose=h>=20&&(key=="Notes"||key=="Reply"||key=="Evidence"||key=="Attachment"||
                    key=="TransitReply"||key=="Remedy"||key=="AddressFrom"||key=="AddressTo");
                if(prose)PostalType.Text(g,value,chineseStyle?PostalType.Fang:PostalType.Song,fill,8,false,false,true);
                else Value(g,key,value,fill,h<10.5f?6.2f:8,centered);
            }
        }

        public static void Value(Graphics g,string key,string value,RectangleF area,float size,bool center=false,bool bold=false)
        {
            bool numeric=(key=="Bag"||key=="BagNo"||key=="Dispatch"||key=="Tracking"||key=="Reference"||
                          key=="Card"||key=="ItemId"||key=="Qso"||key=="Date")&&value.All(c=>c<128);
            string face=numeric?PostalType.Number:PostalType.Song;
            PostalType.Text(g,value,face,area,size,bold,center);
        }

        public static void Part(Graphics g,string id,string cn,string number,float x,float y,float w,float h,bool chineseStyle=false)
        {
            using(var brush=new SolidBrush(Color.FromArgb(243,243,243)))g.FillRectangle(brush,x,y,w,h);
            Rule(g,x,y,x+w,y,.29f);
            Rule(g,x,y+h,x+w,y+h,.16f);
            float inset=number.Length>0?10:1.1f;
            if(number.Length>0)
            {
                PostalType.Text(g,number,PostalType.Serif,Area(x+1,y+.3f,7.9f,h-.6f),8,true,true);
                Rule(g,x+9,y,x+9,y+h,.15f);
            }
            PostalType.Caption(g,PostalType.Label(id,cn),Area(x+inset,y+.46f,w-inset-1.2f,h-.92f),7.3f,false,chineseStyle,true);
        }

        public static void Stamp(Graphics g,float x,float y,float d,string id,string chinese,bool chineseStyle)
        {
            using(var pen=new Pen(Color.FromArgb(78,78,78),.15f))
            {
                pen.DashStyle=DashStyle.Dash;
                g.DrawEllipse(pen,x,y,d,d);
            }
            PostalType.Caption(g,PostalType.Label(id,chinese),Area(x-6,y+d+.5f,d+12,5.1f),5.5f,true,chineseStyle);
        }

        public static void Signature(Graphics g,string id,string cn,float x,float y,float w,bool chineseStyle=false)
        {
            Rule(g,x,y,x+w,y,.16f);
            if(w<35&&id=="FromSign")id="FromSignShort";
            if(w<35&&id=="ToSign")id="ToSignShort";
            PostalType.Caption(g,PostalType.Label(id,cn),Area(x,y+.25f,w,5.75f),5.8f,true,chineseStyle);
        }

        public static void Choices(Graphics g,RectangleF area, bool chineseStyle, params string[] keysAndText)
        {
            ChoicesWithSelection(g,area,chineseStyle,"",keysAndText);
        }

        public static void ChoicesWithSelection(Graphics g,RectangleF area,bool chineseStyle,string selected,params string[] keysAndText)
        {
            int options=keysAndText.Length/2;
            for(int i=0;i<options;i++)
            {
                float w=area.Width/options;
                float x=area.X+i*w;
                Frame(g,Area(x+.9f,area.Y+1.3f,2.6f,2.6f),.17f);
                if(selected==keysAndText[2*i])
                {
                    Rule(g,x+1.25f,area.Y+2.61f,x+1.90f,area.Y+3.38f,.23f);
                    Rule(g,x+1.90f,area.Y+3.38f,x+3.16f,area.Y+1.69f,.23f);
                }
                PostalType.Caption(g,PostalType.Label(keysAndText[2*i],keysAndText[2*i+1]),
                    Area(x+4.1f,area.Y+.35f,w-4.6f,area.Height-.7f),6.3f,false,chineseStyle);
            }
        }

        public static void TagOutline(Graphics g,float w,float h,float gutter)
        {
            var edge=new[]{new PointF(2.4f,11.6f),new PointF(11.6f,2.4f),new PointF(w-2.4f,2.4f),
                new PointF(w-2.4f,h-3.2f),new PointF(11.6f,h-3.2f),new PointF(2.4f,h-12.4f)};
            using(var border=new Pen(Color.Black,.23f))g.DrawPolygon(border,edge);
            using(var pen=new Pen(Color.Black,.17f))g.DrawEllipse(pen,5.4f,h*.48f,4.2f,4.2f);
            var state=g.Save();
            try {g.TranslateTransform(gutter-3.5f,h*.60f);g.RotateTransform(-90);PostalType.Text(g,"QSL",PostalType.Sans,Area(0,0,13,4.7f),7.7f,true,true);}
            finally { g.Restore(state); }
        }

        public static void Footnote(Graphics g,float w,float h,string code,string ending,bool chineseStyle=false)
        {
            var title=PostalType.Label("Internal","业余无线电 QSL 交换");
            string line=title.Chinese+" · "+title.French+" · "+title.English;
            if(code.Length>0)line=code+"   "+line;
            if(ending.Length>0)line+="   "+ending;
            PostalType.Text(g,line,chineseStyle?PostalType.Serif:PostalType.Sans,Area(2.8f,h-4.3f,w-5.6f,3),5.1f,false,true);
        }
    }
}
