namespace PhysicalAssembly
{
    // Distance hysteresis also works when a provider supplies joints but no pinch flag.
    public sealed class PinchLatch
    {
        public bool IsPinching { get; private set; }
        // Whether a pinch would count now: after tracking starts or returns, the hand must first read this open.
        public bool Armed => armed;
        // How open (normalized tip distance) that is. The release distance by default; a game whose players rest their
        // hands half-closed can lower it, but keep it above the .028 close distance.
        public float ArmDistance=.045f;
        bool armed;
        public bool Update(bool valid,float normalizedTipDistance,bool systemPinch)
        {
            if(!valid) { Reset(); return false; }
            if(!armed)
            {
                if(normalizedTipDistance>=ArmDistance) armed=true;
                return false;
            }
            if(IsPinching) IsPinching=normalizedTipDistance<.045f;
            else IsPinching=normalizedTipDistance<=.028f || (systemPinch && normalizedTipDistance<.04f);
            return IsPinching;
        }
        public void Reset() { armed=false; IsPinching=false; }
    }
}
