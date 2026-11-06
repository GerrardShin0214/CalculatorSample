using System.Collections.Generic;
using KitchenDefense.Core;
using NUnit.Framework;

namespace KitchenDefense.Tests
{
    /// <summary>이야기 데이터가 시즌 구조·동물 등장 순서와 맞는지 확인한다.</summary>
    public class StoryTests
    {
        static IEnumerable<StoryBeat> AllBeats()
        {
            for (int n = 0; n < Balance.SeasonDays; n++) yield return Story.BeforeNight(n);
            foreach (var rank in new[] { "S", "A", "B", "C" }) yield return Story.Ending(rank);
            yield return Story.Failure();
        }

        [Test]
        public void EveryNightOfTheSeason_HasAStoryBeat()
        {
            Assert.AreEqual(Balance.SeasonDays, Story.BeatCount);
            for (int n = 0; n < Balance.SeasonDays; n++)
            {
                var beat = Story.BeforeNight(n);
                Assert.IsNotNull(beat, n + "일 밤");
                Assert.IsFalse(string.IsNullOrEmpty(beat.title));
                Assert.IsFalse(string.IsNullOrEmpty(beat.goal));
                Assert.GreaterOrEqual(beat.lines.Length, 3);
            }
            Assert.IsNull(Story.BeforeNight(Balance.SeasonDays));
        }

        [Test]
        public void EveryLine_HasTextAndAKnownSpeaker()
        {
            foreach (var beat in AllBeats())
                foreach (var line in beat.lines)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(line.text), beat.id);
                    if (!line.IsNarration) Assert.IsNotNull(Story.Speaker(line.speaker), beat.id + ": " + line.speaker);
                }
        }

        [Test]
        public void EachAnimal_IsIntroducedInTheStoryOfItsFirstNight()
        {
            foreach (var def in GameData.Monsters)
            {
                if (def.type == MonsterType.Mouse) continue; // 생쥐 떼가 흩어진 것
                string keyword = def.name.Split(' ')[0];
                var beat = Story.BeforeNight(def.introNight);
                bool mentioned = false;
                foreach (var line in beat.lines) mentioned |= line.text.Contains(keyword);
                mentioned |= beat.goal.Contains(keyword);
                Assert.IsTrue(mentioned, def.name + "은(는) " + def.introNight + "일 밤 이야기에서 소개돼야 한다");
            }
        }

        [Test]
        public void Waves_OnlyBringAnimalsAlreadyIntroduced()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var s = GameSession.NewSeason(seed);
                s.AcceptOffer();
                for (int night = 0; night < Balance.SeasonDays; night++)
                {
                    s.Run.day = night;
                    s.Run.phase = GamePhase.Intro;
                    s.Run.reservation = night == 0 ? s.Run.reservation : DayLogic.RollReservation(s.Run, night + 1);
                    s.StartNight();
                    foreach (var wave in s.Night.State.waves)
                        foreach (var spawn in wave.spawns)
                            Assert.LessOrEqual(GameData.Monster(spawn.type).introNight, night,
                                GameData.Monster(spawn.type).name + "이(가) " + night + "일 밤에 너무 일찍 나왔다");
                }
            }
        }

        [Test]
        public void CriticCountdown_EndsOnTheLastDay()
        {
            Assert.AreEqual(Balance.SeasonDays, Story.DaysUntilCritic(0));
            Assert.AreEqual(1, Story.DaysUntilCritic(Balance.SeasonDays - 1));
        }
    }
}
