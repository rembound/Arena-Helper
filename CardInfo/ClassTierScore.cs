using ArenaHelper.Enums;

namespace ArenaHelper.CardInfo
{
    public class ClassTierScore
    {
        public HeroClass? Hero { get; set; }
        public double Score { get; set; }
        public bool StopAfterFirst { get; set; }
        public bool StopAfterSecond { get; set; }
    }
}