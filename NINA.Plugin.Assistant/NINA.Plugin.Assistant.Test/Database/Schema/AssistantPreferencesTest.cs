﻿using Assistant.NINAPlugin.Astrometry;
using Assistant.NINAPlugin.Database.Schema;
using FluentAssertions;
using NUnit.Framework;

namespace NINA.Plugin.Assistant.Test.Database.Schema {

    [TestFixture]
    public class PreferencesTest {

        [Test]
        public void TestProjectPreferencesDefaults() {
            var sut = new Project("123");
            sut.MinimumTime.Should().Be(30);
            sut.MinimumAltitude.Should().BeApproximately(0, 0.0001);
            sut.UseCustomHorizon.Should().BeFalse();
            sut.HorizonOffset.Should().BeApproximately(0, 0.0001);
            sut.DitherEvery.Should().Be(0);
            sut.EnableGrader.Should().BeTrue();
            sut.RuleWeights.Should().NotBeNull();
        }

        [Test]
        public void TestFilterPreferencesOrder() {
            Assert.IsTrue(TwilightLevel.Nighttime < TwilightLevel.Astronomical);
            Assert.IsTrue(TwilightLevel.Astronomical < TwilightLevel.Nautical);
            Assert.IsTrue(TwilightLevel.Nautical < TwilightLevel.Civil);
        }

        [Test]
        public void TestFilterPreferencesDefaults() {
            var sut = new FilterPreference("123", "L");
            sut.TwilightLevel.Should().Be(TwilightLevel.Nighttime);
            sut.MoonAvoidanceEnabled.Should().BeFalse();
            sut.MoonAvoidanceSeparation.Should().BeApproximately(60, 0.0001);
            sut.MoonAvoidanceWidth.Should().Be(7);
            sut.MaximumHumidity.Should().BeApproximately(0, 00001);
        }

        [Test]
        public void TestFilterPreferencesTwilightChecks() {
            var sut = new FilterPreference("123", "L");

            sut.TwilightLevel = TwilightLevel.Nighttime;
            sut.IsTwilightNightOnly().Should().BeTrue();
            sut.IsTwilightAstronomical().Should().BeFalse();
            sut.IsTwilightNautical().Should().BeFalse();
            sut.IsTwilightCivil().Should().BeFalse();

            sut.TwilightLevel = TwilightLevel.Astronomical;
            sut.IsTwilightNightOnly().Should().BeFalse();
            sut.IsTwilightAstronomical().Should().BeTrue();
            sut.IsTwilightNautical().Should().BeFalse();
            sut.IsTwilightCivil().Should().BeFalse();

            sut.TwilightLevel = TwilightLevel.Nautical;
            sut.IsTwilightNightOnly().Should().BeFalse();
            sut.IsTwilightAstronomical().Should().BeFalse();
            sut.IsTwilightNautical().Should().BeTrue();
            sut.IsTwilightCivil().Should().BeFalse();

            sut.TwilightLevel = TwilightLevel.Civil;
            sut.IsTwilightNightOnly().Should().BeFalse();
            sut.IsTwilightAstronomical().Should().BeFalse();
            sut.IsTwilightNautical().Should().BeFalse();
            sut.IsTwilightCivil().Should().BeTrue();
        }
    }

}