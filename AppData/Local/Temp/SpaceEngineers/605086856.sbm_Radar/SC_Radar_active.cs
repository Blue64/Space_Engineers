using AnimaScript;

namespace AnimaData
{
    public class Seq_SC_Radar_active : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_active());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_active",0,60,24f,0,0);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part1_active : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part1_active());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part1_active",0,60,24f,0,60);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.052336f,0f,0.99863f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.104528f,0f,0.994522f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.156434f,0f,0.987688f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.207912f,0f,0.978148f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.258819f,0f,0.965926f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.309017f,0f,0.951057f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.358368f,0f,0.93358f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.406737f,0f,0.913545f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.453991f,0f,0.891007f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.5f,0f,0.866025f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.544639f,0f,0.838671f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.587785f,0f,0.809017f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.62932f,0f,0.777146f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.669131f,0f,0.743145f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.707107f,0f,0.707107f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.743145f,0f,0.669131f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.777146f,0f,0.62932f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.809017f,0f,0.587785f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.838671f,0f,0.544639f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.866025f,0f,0.5f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.891007f,0f,0.45399f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.913546f,0f,0.406737f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.93358f,0f,0.358368f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.951057f,0f,0.309017f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.965926f,0f,0.258819f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.978148f,0f,0.207911f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.987688f,0f,0.156434f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.994522f,0f,0.104528f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0.99863f,0f,0.0523359f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,1f,0f,0f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.99863f,0f,0.0523351f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.994522f,0f,0.104529f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.987688f,0f,0.156434f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.978148f,0f,0.207912f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.965926f,0f,0.258819f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.951057f,0f,0.309017f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.93358f,0f,0.358368f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.913545f,0f,0.406737f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.891006f,0f,0.453991f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.866025f,0f,0.5f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.83867f,0f,0.544639f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.809017f,0f,0.587785f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.777146f,0f,0.629321f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.743145f,0f,0.669131f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.707107f,0f,0.707107f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.669131f,0f,0.743145f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.62932f,0f,0.777146f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.587785f,0f,0.809017f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.544639f,0f,0.838671f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.5f,0f,0.866026f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.45399f,0f,0.891007f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.406737f,0f,0.913545f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.358368f,0f,0.933581f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.309017f,0f,0.951057f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.258819f,0f,0.965926f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.207911f,0f,0.978148f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.156434f,0f,0.987688f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.104528f,0f,0.994522f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,-0.0523358f,0f,0.99863f,1f,1f,1f);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part2_active : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part2_active());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part2_active",0,60,24f,0,0);
            PLocRotScale(-0f,0.3f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part3_active : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part3_active());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part3_active",0,60,24f,0,0);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.382683f,0.92388f,1f,1f,1f);
        }
    }
}
