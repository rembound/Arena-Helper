using ArenaHelper.Enums;
using System.Collections.Generic;

namespace ArenaHelper.CardInfo
{
    public class CardTierInfo
    {
        public string CardId { get; set; }
        public int Cost { get; set; }
        public HeroClass? Hero { get; set; }
        public string Name { get; set; }
        public bool NewCard { get; set; }
        public CardRarity Rarity { get; set; }
        public List<ClassTierScore> Scores { get; set; }
    }
}