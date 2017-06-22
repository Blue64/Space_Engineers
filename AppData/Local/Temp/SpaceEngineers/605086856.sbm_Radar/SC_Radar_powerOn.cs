using AnimaScript;

namespace AnimaData
{
    public class Seq_SC_Radar_powerOn : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_powerOn());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_powerOn",0,60,24f,0,0);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part1_powerOn : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part1_powerOn());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part1_powerOn",0,60,24f,0,60);
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
    public class Seq_SC_Radar_Part2_powerOn : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part2_powerOn());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part2_powerOn",0,60,24f,0,60);
            PLocRotScale(-0f,0f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.005f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.01f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.015f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.02f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.025f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.03f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.035f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.04f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.045f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.05f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.055f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.06f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.065f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.07f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.075f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.08f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.085f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.09f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.095f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.1f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.105f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.11f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.115f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.12f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.125f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.13f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.135f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.14f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.145f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.15f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.155f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.16f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.165f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.17f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.175f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.18f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.185f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.19f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.195f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.2f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.205f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.21f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.215f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.22f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.225f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.23f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.235f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.24f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.245f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.25f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.255f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.26f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.265f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.27f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.275f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.28f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.285f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.29f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.295f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,0.3f,0f,-0f,0f,0f,1f,1f,1f,1f);
        }
    }
    public class Seq_SC_Radar_Part3_powerOn : AnimaSeqBase
    {
        private static AnimaSeqBase m_seq = null;
        new public static AnimaSeqBase Adquire()
        {
            if (m_seq == null) m_seq = PManAdd(new Seq_SC_Radar_Part3_powerOn());
            return m_seq;
        }
        public override void DiscardStatic() { m_seq = PManRem(m_seq); }
        new public static void Discard() { m_seq = PManRem(m_seq); }
        protected override void PData()
        {
            PInit("Seq_SC_Radar_Part3_powerOn",0,60,24f,0,60);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0f,1f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.00654494f,0.999979f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0130896f,0.999914f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0196337f,0.999807f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.026177f,0.999657f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0327191f,0.999465f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0392598f,0.999229f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0457989f,0.998951f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.052336f,0.99863f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0588708f,0.998266f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0654031f,0.997859f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0719327f,0.99741f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0784591f,0.996917f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0849822f,0.996382f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0915016f,0.995805f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.0980172f,0.995185f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.104528f,0.994522f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.111035f,0.993816f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.117537f,0.993068f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.124034f,0.992278f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.130526f,0.991445f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.137012f,0.990569f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.143493f,0.989651f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.149967f,0.988691f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.156435f,0.987688f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.162896f,0.986643f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.16935f,0.985556f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.175796f,0.984427f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.182236f,0.983255f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.188667f,0.982041f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.19509f,0.980785f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.201505f,0.979487f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.207912f,0.978148f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.214309f,0.976766f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.220697f,0.975342f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.227076f,0.973877f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.233445f,0.97237f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.239804f,0.970821f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.246153f,0.969231f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.252492f,0.967599f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.258819f,0.965926f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.265135f,0.964211f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.271441f,0.962455f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.277734f,0.960658f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.284015f,0.95882f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.290285f,0.95694f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.296542f,0.95502f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.302786f,0.953059f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.309017f,0.951057f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.315235f,0.949014f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.32144f,0.94693f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.32763f,0.944806f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.333807f,0.942641f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.339969f,0.940437f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.346117f,0.938191f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.35225f,0.935906f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.358368f,0.93358f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.364471f,0.931215f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.370557f,0.92881f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.376629f,0.926364f,1f,1f,1f);
            PLocRotScale(-0f,-0.975f,0f,-0f,0f,0.382683f,0.92388f,1f,1f,1f);
        }
    }
}
