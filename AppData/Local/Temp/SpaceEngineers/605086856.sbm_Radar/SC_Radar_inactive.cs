using AnimaScript;

namespace AnimaData
{
    public class Seq_SC_Radar_inactive : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_inactive());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_inactive",0,60,24f,0,0);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part1_inactive : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part1_inactive());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part1_inactive",0,60,24f,0,0);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part2_inactive : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part2_inactive());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part2_inactive",0,60,24f,0,0);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part3_inactive : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part3_inactive());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part3_inactive",0,60,24f,0,0);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
}
