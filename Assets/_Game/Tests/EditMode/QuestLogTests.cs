using System.Collections.Generic;
using Arcanum.Formats.Quest;
using Arcanum.Formats.Text;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Quest data parse: <c>gamequest.mes</c> metadata (XP level, alignment shift, the three <c>Q:</c> dialog
    /// banks × 7 states), the XP-table join, the journal-description (+ dumb fallback), and the botched modifier.
    /// </summary>
    public sealed class QuestLogTests
    {
        private static MesFile Mes(params (int k, string v)[] e)
        {
            var list = new List<KeyValuePair<int, string>>();
            foreach (var (k, v) in e) list.Add(new KeyValuePair<int, string>(k, v));
            return new MesFile(list);
        }

        // "expLevel alignAdj  <7 normal>  <7 bad-reaction>  <7 dumb>"
        private const string Quest1000 =
            "5 -10  100 101 102 103 104 105 106  200 201 202 203 204 205 206  300 301 302 303 304 305 306";

        [Test]
        public void ParsesGameQuestMetadataAndXp()
        {
            var log = QuestLog.FromMes(null, xpQuest: Mes((5, "1500")), gameQuest: Mes((1000, Quest1000)));

            QuestInfo q = log.Meta(1000);
            Assert.That(q, Is.Not.Null);
            Assert.That(q.ExperienceLevel, Is.EqualTo(5));
            Assert.That(q.AlignmentAdjustment, Is.EqualTo(-10));
            Assert.That(log.QuestXp(1000), Is.EqualTo(1500));        // xp_quest[experience_level]
            Assert.That(log.AlignmentAdjustment(1000), Is.EqualTo(-10));
        }

        [Test]
        public void DialogEntryByStateAndBank()
        {
            var log = QuestLog.FromMes(null, gameQuest: Mes((1000, Quest1000)));
            Assert.That(log.DialogEntry(1000, QuestState.Accepted), Is.EqualTo(102));                       // normal[Accepted]
            Assert.That(log.DialogEntry(1000, QuestState.Achieved, DialogBank.BadReaction), Is.EqualTo(203));
            Assert.That(log.DialogEntry(1000, QuestState.Botched, DialogBank.Dumb), Is.EqualTo(306));
            Assert.That(log.DialogEntry(9999, QuestState.Accepted), Is.EqualTo(-1));                        // unknown quest
        }

        [Test]
        public void BotchedModifier()
        {
            int achievedBotched = (int)QuestState.Achieved | QuestLog.BotchedModifier;
            Assert.That(QuestLog.IsBotched(achievedBotched), Is.True);
            Assert.That(QuestLog.IsBotched((int)QuestState.Achieved), Is.False);
            Assert.That(QuestLog.StripBotched(achievedBotched), Is.EqualTo(QuestState.Achieved));
            Assert.That(QuestLog.WithBotched((int)QuestState.Achieved), Is.EqualTo(achievedBotched));
            Assert.That(QuestLog.IsKnown(achievedBotched), Is.True);                 // known through the modifier
            Assert.That(QuestLog.IsKnown((int)QuestState.Unknown), Is.False);
            Assert.That(QuestLog.IsKnown((int)QuestState.Mentioned), Is.True);
        }

        [Test]
        public void DescriptionWithDumbFallback()
        {
            var log = QuestLog.FromMes(
                Mes((1000, "Find the sword."), (1001, "Slay the dragon.")),
                dumbDescriptions: Mes((1000, "Get pointy thing.")));
            Assert.That(log.Description(1000), Is.EqualTo("Find the sword."));
            Assert.That(log.Description(1000, dumb: true), Is.EqualTo("Get pointy thing."));
            Assert.That(log.Description(1001, dumb: true), Is.EqualTo("Slay the dragon.")); // no dumb entry → normal text
            Assert.That(log.Description(9999), Is.Null);
        }
    }
}
