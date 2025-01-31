﻿using Assistant.NINAPlugin.Astrometry;
using Assistant.NINAPlugin.Controls.AssistantManager;
using Assistant.NINAPlugin.Database.Schema;
using Assistant.NINAPlugin.Plan;
using FluentAssertions;
using Moq;
using NINA.Plugin.Assistant.Test.Astrometry;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.Assistant.Test.Plan {

    [TestFixture]
    public class ExposurePlannerTest {

        [Test]
        public void testTypical() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            int nbExposures = 50;
            int nbExposureLength = 180;
            int wbExposures = 10;
            int wbExposureLength = 120;

            Mock<IPlanProject> pp = GetTestProject(dateTime, 0, nbExposures, nbExposureLength, wbExposures, wbExposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            TimeInterval window = new TimeInterval((DateTime)ntc.AstronomicalTwilightStart, (DateTime)ntc.AstronomicalTwilightEnd);
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();
            AssertPlan(GetExpectedTestTypical(ntc), list);

            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[4].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[5].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[6].PlannedExposures, Is.EqualTo(wbExposures));
        }

        [Test]
        public void testTypicalFilterSwitch2() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            int nbExposures = 50;
            int nbExposureLength = 180;
            int wbExposures = 10;
            int wbExposureLength = 120;

            Mock<IPlanProject> pp = GetTestProject(dateTime, 2, nbExposures, nbExposureLength, wbExposures, wbExposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            TimeInterval window = new TimeInterval((DateTime)ntc.AstronomicalTwilightStart, ((DateTime)ntc.NighttimeStart).AddMinutes(60));
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();
            AssertPlan(GetExpectedTestTypicalFS2(ntc), list);

            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(11));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(9));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(8));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(4));
            Assert.That(pt.ExposurePlans[4].PlannedExposures, Is.EqualTo(4));
            Assert.That(pt.ExposurePlans[5].PlannedExposures, Is.EqualTo(4));
            Assert.That(pt.ExposurePlans[6].PlannedExposures, Is.EqualTo(4));
        }

        [Test]
        public void testWindowNotFilled() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            int nbExposures = 5;
            int nbExposureLength = 60;
            int wbExposures = 10;
            int wbExposureLength = 120;

            Mock<IPlanProject> pp = GetTestProject(dateTime, 0, nbExposures, nbExposureLength, wbExposures, wbExposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            TimeInterval window = new TimeInterval((DateTime)ntc.AstronomicalTwilightStart, ((DateTime)ntc.NighttimeStart).AddMinutes(60));
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();
            AssertPlan(GetExpectedWindowNotFilled(ntc), list);

            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[4].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[5].PlannedExposures, Is.EqualTo(9));
            Assert.That(pt.ExposurePlans[6].PlannedExposures, Is.EqualTo(0));
        }

        [Test]
        public void testNoNightAtDusk() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNoNight(dateTime);

            int exposures = 20;
            int exposureLength = 180;

            Mock<IPlanProject> pp = GetHighLatitudeTestProject(dateTime, 0, exposures, exposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            TimeInterval window = new TimeInterval(ntc.CivilTwilightStart, ((DateTime)ntc.AstronomicalTwilightStart).AddHours(2));
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();
            AssertPlan(GetExpectedTestNoNightAtDusk(ntc), list);

            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(exposures));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(exposures));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(exposures));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(0));
        }

        [Test]
        public void testNoNightAtDawn() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNoNight(dateTime);

            int exposures = 20;
            int exposureLength = 180;

            Mock<IPlanProject> pp = GetHighLatitudeTestProject(dateTime, 0, exposures, exposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            TimeInterval window = new TimeInterval(dateTime.Date.AddDays(1).AddHours(4), ntc.CivilTwilightEnd);
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();
            AssertPlan(GetExpectedTestNoNightAtDawn(ntc), list);

            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(exposures));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(18));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(0));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(0));
        }

        [Test]
        public void testExposurePlanMixRejected() {
            DateTime dateTime = DateTime.Now;

            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            Mock<IPlanProject> pp = PlanMocks.GetMockPlanProject("pp", ProjectState.Active);
            pp.Object.FilterSwitchFrequency = 0;
            Mock<IPlanTarget> pt = PlanMocks.GetMockPlanTarget("pt", TestUtil.M42);
            PlanMocks.AddMockPlanTarget(pp, pt);

            Mock<IPlanExposure> pe = PlanMocks.GetMockPlanExposure("O3", 1, 0, 60);
            pe.Object.TwilightLevel = TwilightLevel.Nighttime;
            pe.Object.Rejected = false;
            PlanMocks.AddMockPlanFilter(pt, pe);

            pe = PlanMocks.GetMockPlanExposure("R", 1, 0, 60);
            pe.Object.TwilightLevel = TwilightLevel.Nighttime;
            pe.Object.Rejected = true;
            pe.Object.RejectedReason = Reasons.TargetMoonAvoidance;
            PlanMocks.AddMockPlanFilter(pt, pe);

            TimeInterval window = new TimeInterval((DateTime)ntc.NighttimeStart, (DateTime)ntc.NighttimeEnd);
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt.Object, window, ntc).Plan();
            AssertPlan(GetExpectedPlanMixRejected(), list);
        }

        [Test]
        public void testDither() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            int nbExposures = 0;
            int nbExposureLength = 60;
            int wbExposures = 4;
            int wbExposureLength = 120;

            Mock<IPlanProject> pp = GetTestProject(dateTime, 0, nbExposures, nbExposureLength, wbExposures, wbExposureLength);
            pp.SetupProperty(p => p.FilterSwitchFrequency, 1);
            pp.SetupProperty(p => p.DitherEvery, 3);
            IPlanTarget pt = pp.Object.Targets[0];

            TimeInterval window = new TimeInterval((DateTime)ntc.NighttimeStart, ((DateTime)ntc.NighttimeStart).AddMinutes(60));
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();

            AssertPlan(GetExpectedDither(ntc), list);
        }

        [Test]
        public void testCleanup() {
            List<IPlanInstruction> list = new List<IPlanInstruction>();

            ExposurePlanner.Cleanup(list).Count.Should().Be(0);

            list.Add(new PlanWait(DateTime.Now));
            list.Add(new PlanWait(DateTime.Now));
            list.Add(new PlanWait(DateTime.Now));
            ExposurePlanner.Cleanup(list).Count.Should().Be(3);

            list.Clear();
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanWait(DateTime.Now));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanWait(DateTime.Now));
            ExposurePlanner.Cleanup(list).Count.Should().Be(4);

            list.Clear();
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanWait(DateTime.Now));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanWait(DateTime.Now));
            ExposurePlanner.Cleanup(list).Count.Should().Be(4);

            list.Clear();
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanWait(DateTime.Now));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanSwitchFilter(null));
            list.Add(new PlanWait(DateTime.Now));
            list.Add(new PlanSwitchFilter(null));
            ExposurePlanner.Cleanup(list).Count.Should().Be(4);
        }

        [Test]
        public void testOverrideExposureOrder1() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            int nbExposures = 50;
            int nbExposureLength = 180;
            int wbExposures = 10;
            int wbExposureLength = 120;

            Mock<IPlanProject> pp = GetTestProject(dateTime, 0, nbExposures, nbExposureLength, wbExposures, wbExposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            int id = 0;
            foreach (IPlanExposure pe in pt.ExposurePlans) {
                pe.DatabaseId = id++;
            }

            string[] items = { "0", "1", "2", OverrideExposureOrder.DITHER, "3", "4", "5", "6", OverrideExposureOrder.DITHER };
            pt.OverrideExposureOrder = string.Join(OverrideExposureOrder.SEP, items);

            TimeInterval window = new TimeInterval((DateTime)ntc.AstronomicalTwilightStart, (DateTime)ntc.AstronomicalTwilightEnd);
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();

            AssertPlan(GetExpectedTestOverride1(ntc), list);
            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[4].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[5].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[6].PlannedExposures, Is.EqualTo(wbExposures));
        }

        [Test]
        public void testOverrideExposureOrder2() {
            DateTime dateTime = DateTime.Now;
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            int nbExposures = 12;
            int nbExposureLength = 180;
            int wbExposures = 6;
            int wbExposureLength = 120;

            Mock<IPlanProject> pp = GetTestProject(dateTime, 0, nbExposures, nbExposureLength, wbExposures, wbExposureLength);
            IPlanTarget pt = pp.Object.Targets[0];

            int id = 0;
            foreach (IPlanExposure pe in pt.ExposurePlans) {
                pe.DatabaseId = id++;
            }

            string[] items = { "0", "0", "1", "1", "2", "2", OverrideExposureOrder.DITHER, "3", "4", "5", "6", OverrideExposureOrder.DITHER };
            pt.OverrideExposureOrder = string.Join(OverrideExposureOrder.SEP, items);

            TimeInterval window = new TimeInterval((DateTime)ntc.AstronomicalTwilightStart, (DateTime)ntc.AstronomicalTwilightEnd);
            List<IPlanInstruction> list = new ExposurePlanner(GetPrefs(), pt, window, ntc).Plan();

            AssertPlan(GetExpectedTestOverride2(ntc), list);
            Assert.That(pt.ExposurePlans[0].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[1].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[2].PlannedExposures, Is.EqualTo(nbExposures));
            Assert.That(pt.ExposurePlans[3].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[4].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[5].PlannedExposures, Is.EqualTo(wbExposures));
            Assert.That(pt.ExposurePlans[6].PlannedExposures, Is.EqualTo(wbExposures));
        }

        private ProfilePreference GetPrefs(string profileId = "abcd-1234") {
            return new ProfilePreference(profileId);
        }

        private Mock<IPlanProject> GetTestProject(DateTime dateTime, int filterSwitchFrequency, int nbExposures, int nbExposureLength, int wbExposures, int wbExposureLength) {
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            Mock<IPlanProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.Object.FilterSwitchFrequency = filterSwitchFrequency;
            Mock<IPlanTarget> pt = PlanMocks.GetMockPlanTarget("M42", TestUtil.M42);
            PlanMocks.AddMockPlanTarget(pp, pt);

            Mock<IPlanExposure> pf = PlanMocks.GetMockPlanExposure("Ha", nbExposures, 0, nbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Astronomical;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("OIII", nbExposures, 0, nbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Astronomical;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("SII", nbExposures, 0, nbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Astronomical;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("L", wbExposures, 0, wbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Nighttime;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("R", wbExposures, 0, wbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Nighttime;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("G", wbExposures, 0, wbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Nighttime;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("B", wbExposures, 0, wbExposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Nighttime;
            PlanMocks.AddMockPlanFilter(pt, pf);

            return pp;
        }

        private Mock<IPlanProject> GetHighLatitudeTestProject(DateTime dateTime, int filterSwitchFrequency, int exposures, int exposureLength) {
            TestNighttimeCircumstances ntc = TestNighttimeCircumstances.GetNormal(dateTime);

            Mock<IPlanProject> pp = PlanMocks.GetMockPlanProject("pp1", ProjectState.Active);
            pp.Object.FilterSwitchFrequency = filterSwitchFrequency;
            Mock<IPlanTarget> pt = PlanMocks.GetMockPlanTarget("M42", TestUtil.M42);
            PlanMocks.AddMockPlanTarget(pp, pt);

            Mock<IPlanExposure> pf = PlanMocks.GetMockPlanExposure("Civil", exposures, 0, exposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Civil;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("Nautical", exposures, 0, exposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Nautical;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("Astro", exposures, 0, exposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Astronomical;
            PlanMocks.AddMockPlanFilter(pt, pf);
            pf = PlanMocks.GetMockPlanExposure("Night", exposures, 0, exposureLength);
            pf.Object.TwilightLevel = TwilightLevel.Nighttime;
            PlanMocks.AddMockPlanFilter(pt, pf);

            return pp;
        }

        private void AssertPlan(List<IPlanInstruction> expectedPlan, List<IPlanInstruction> actualPlan) {
            /*
            TestContext.WriteLine("EXPECTED:");
            for (int i = 0; i < expectedPlan.Count; i++) {
                TestContext.WriteLine($"{expectedPlan[i]}");
            }

            TestContext.WriteLine("\nACTUAL:");
            for (int i = 0; i < actualPlan.Count; i++) {
                TestContext.WriteLine($"{actualPlan[i]}");
            }
            */

            Assert.That(actualPlan.Count, Is.EqualTo(expectedPlan.Count));

            for (int i = 0; i < expectedPlan.Count; i++) {
                IPlanInstruction expected = expectedPlan[i];
                IPlanInstruction actual = actualPlan[i];
                //TestContext.Error.WriteLine($"i {i}");

                Assert.That(expected.GetType(), Is.EqualTo(actual.GetType()));

                if (expected is PlanMessage) {
                    continue;
                }

                if (expected is PlanSwitchFilter) {
                    Assert.That(actual.planExposure.FilterName, Is.EqualTo(expected.planExposure.FilterName));
                    continue;
                }

                if (expected is PlanSetReadoutMode) {
                    Assert.That(actual.planExposure.ReadoutMode, Is.EqualTo(expected.planExposure.ReadoutMode));
                    continue;
                }

                if (expected is PlanTakeExposure) {
                    Assert.That(actual.planExposure.FilterName, Is.EqualTo(expected.planExposure.FilterName));
                    continue;
                }

                if (expected is PlanWait) {
                    Assert.That(((PlanWait)actual).waitForTime, Is.EqualTo(((PlanWait)expected).waitForTime));
                    continue;
                }

                if (expected is PlanDither) {
                    continue;
                }

                throw new AssertionException($"unknown actual instruction type: {actual.GetType().FullName}");
            }
        }

        private List<IPlanInstruction> GetExpectedTestTypical(NighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Ha = PlanMocks.GetMockPlanExposure("Ha", 10, 0, 180);
            Mock<IPlanExposure> OIII = PlanMocks.GetMockPlanExposure("OIII", 10, 0, 180);
            Mock<IPlanExposure> SII = PlanMocks.GetMockPlanExposure("SII", 10, 0, 180);
            Mock<IPlanExposure> L = PlanMocks.GetMockPlanExposure("L", 10, 0, 120);
            Mock<IPlanExposure> R = PlanMocks.GetMockPlanExposure("R", 10, 0, 120);
            Mock<IPlanExposure> G = PlanMocks.GetMockPlanExposure("G", 10, 0, 120);
            Mock<IPlanExposure> B = PlanMocks.GetMockPlanExposure("B", 10, 0, 120);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));

            AddActualExposures(actual, Ha.Object, 19);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, L.Object, 10);
            AddActualExposures(actual, R.Object, 10);
            AddActualExposures(actual, G.Object, 10);
            AddActualExposures(actual, B.Object, 10);
            AddActualExposures(actual, Ha.Object, 31);
            AddActualExposures(actual, OIII.Object, 50);
            AddActualExposures(actual, SII.Object, 32);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, SII.Object, 18);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedTestTypicalFS2(NighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Ha = PlanMocks.GetMockPlanExposure("Ha", 10, 0, 180);
            Mock<IPlanExposure> OIII = PlanMocks.GetMockPlanExposure("OIII", 10, 0, 180);
            Mock<IPlanExposure> SII = PlanMocks.GetMockPlanExposure("SII", 10, 0, 180);
            Mock<IPlanExposure> L = PlanMocks.GetMockPlanExposure("L", 10, 0, 120);
            Mock<IPlanExposure> R = PlanMocks.GetMockPlanExposure("R", 10, 0, 120);
            Mock<IPlanExposure> G = PlanMocks.GetMockPlanExposure("G", 10, 0, 120);
            Mock<IPlanExposure> B = PlanMocks.GetMockPlanExposure("B", 10, 0, 120);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 3; i++) {
                AddActualExposures(actual, Ha.Object, 2);
                AddActualExposures(actual, OIII.Object, 2);
                AddActualExposures(actual, SII.Object, 2);
            }

            AddActualExposures(actual, Ha.Object, 1);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, L.Object, 2);
            AddActualExposures(actual, R.Object, 2);
            AddActualExposures(actual, G.Object, 2);
            AddActualExposures(actual, B.Object, 2);
            AddActualExposures(actual, Ha.Object, 2);
            AddActualExposures(actual, OIII.Object, 2);
            AddActualExposures(actual, SII.Object, 2);

            AddActualExposures(actual, L.Object, 2);
            AddActualExposures(actual, R.Object, 2);
            AddActualExposures(actual, G.Object, 2);
            AddActualExposures(actual, B.Object, 2);
            AddActualExposures(actual, Ha.Object, 2);
            AddActualExposures(actual, OIII.Object, 1);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedWindowNotFilled(NighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Ha = PlanMocks.GetMockPlanExposure("Ha", 10, 0, 180);
            Mock<IPlanExposure> OIII = PlanMocks.GetMockPlanExposure("OIII", 10, 0, 180);
            Mock<IPlanExposure> SII = PlanMocks.GetMockPlanExposure("SII", 10, 0, 180);
            Mock<IPlanExposure> L = PlanMocks.GetMockPlanExposure("L", 10, 0, 120);
            Mock<IPlanExposure> R = PlanMocks.GetMockPlanExposure("R", 10, 0, 120);
            Mock<IPlanExposure> G = PlanMocks.GetMockPlanExposure("G", 10, 0, 120);
            Mock<IPlanExposure> B = PlanMocks.GetMockPlanExposure("B", 10, 0, 120);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Ha.Object, 5);
            AddActualExposures(actual, OIII.Object, 5);
            AddActualExposures(actual, SII.Object, 5);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, L.Object, 10);
            AddActualExposures(actual, R.Object, 10);
            AddActualExposures(actual, G.Object, 9);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedTestNoNightAtDusk(TestNighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Civil = PlanMocks.GetMockPlanExposure("Civil", 10, 0, 180);
            Mock<IPlanExposure> Nautical = PlanMocks.GetMockPlanExposure("Nautical", 10, 0, 180);
            Mock<IPlanExposure> Astro = PlanMocks.GetMockPlanExposure("Astro", 10, 0, 180);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Civil.Object, 19);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Civil.Object, 1);
            AddActualExposures(actual, Nautical.Object, 18);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Nautical.Object, 2);
            AddActualExposures(actual, Astro.Object, 20);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedTestNoNightAtDawn(TestNighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Civil = PlanMocks.GetMockPlanExposure("Civil", 10, 0, 180);
            Mock<IPlanExposure> Nautical = PlanMocks.GetMockPlanExposure("Nautical", 10, 0, 180);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Civil.Object, 19);

            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Civil.Object, 1);
            AddActualExposures(actual, Nautical.Object, 18);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedPlanMixRejected() {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Night = PlanMocks.GetMockPlanExposure("O3", 1, 0, 60);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));
            AddActualExposures(actual, Night.Object, 1);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedDither(NighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> L = PlanMocks.GetMockPlanExposure("L", 10, 0, 120);
            Mock<IPlanExposure> R = PlanMocks.GetMockPlanExposure("R", 10, 0, 120);
            Mock<IPlanExposure> G = PlanMocks.GetMockPlanExposure("G", 10, 0, 120);
            Mock<IPlanExposure> B = PlanMocks.GetMockPlanExposure("B", 10, 0, 120);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 3; i++) {
                AddActualExposures(actual, L.Object, 1);
                AddActualExposures(actual, R.Object, 1);
                AddActualExposures(actual, G.Object, 1);
                AddActualExposures(actual, B.Object, 1);
            }

            actual.Add(new PlanSwitchFilter(L.Object));
            actual.Add(new PlanSetReadoutMode(L.Object));
            actual.Add(new PlanDither());
            actual.Add(new PlanTakeExposure(L.Object));
            AddActualExposures(actual, R.Object, 1);
            AddActualExposures(actual, G.Object, 1);
            AddActualExposures(actual, B.Object, 1);

            return actual;
        }

        private List<IPlanInstruction> GetExpectedTestOverride1(TestNighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Ha = PlanMocks.GetMockPlanExposure("Ha", 10, 0, 180);
            Mock<IPlanExposure> OIII = PlanMocks.GetMockPlanExposure("OIII", 10, 0, 180);
            Mock<IPlanExposure> SII = PlanMocks.GetMockPlanExposure("SII", 10, 0, 180);
            Mock<IPlanExposure> L = PlanMocks.GetMockPlanExposure("L", 10, 0, 120);
            Mock<IPlanExposure> R = PlanMocks.GetMockPlanExposure("R", 10, 0, 120);
            Mock<IPlanExposure> G = PlanMocks.GetMockPlanExposure("G", 10, 0, 120);
            Mock<IPlanExposure> B = PlanMocks.GetMockPlanExposure("B", 10, 0, 120);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 6; i++) {
                AddActualExposures(actual, Ha.Object, 1);
                AddActualExposures(actual, OIII.Object, 1);
                AddActualExposures(actual, SII.Object, 1);
                actual.Add(new PlanDither());
            }

            AddActualExposures(actual, Ha.Object, 1);
            actual.Add(new PlanSwitchFilter(OIII.Object));
            actual.Add(new PlanSetReadoutMode(OIII.Object));

            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 10; i++) {
                AddActualExposures(actual, Ha.Object, 1);
                AddActualExposures(actual, OIII.Object, 1);
                AddActualExposures(actual, SII.Object, 1);
                actual.Add(new PlanDither());
                AddActualExposures(actual, L.Object, 1);
                AddActualExposures(actual, R.Object, 1);
                AddActualExposures(actual, G.Object, 1);
                AddActualExposures(actual, B.Object, 1);
                actual.Add(new PlanDither());
            }

            for (int i = 0; i < 27; i++) {
                AddActualExposures(actual, Ha.Object, 1);
                AddActualExposures(actual, OIII.Object, 1);
                AddActualExposures(actual, SII.Object, 1);
                actual.Add(new PlanDither());
            }

            AddActualExposures(actual, Ha.Object, 1);
            AddActualExposures(actual, OIII.Object, 1);
            actual.Add(new PlanSwitchFilter(SII.Object));
            actual.Add(new PlanSetReadoutMode(SII.Object));
            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 5; i++) {
                AddActualExposures(actual, Ha.Object, 1);
                AddActualExposures(actual, OIII.Object, 1);
                AddActualExposures(actual, SII.Object, 1);
                actual.Add(new PlanDither());
            }

            AddActualExposures(actual, OIII.Object, 1);
            AddActualExposures(actual, SII.Object, 1);
            actual.Add(new PlanDither());
            actual.Add(new PlanTakeExposure(SII.Object));
            actual.Add(new PlanDither());

            return actual;
        }

        private List<IPlanInstruction> GetExpectedTestOverride2(TestNighttimeCircumstances ntc) {
            List<IPlanInstruction> actual = new List<IPlanInstruction>();

            Mock<IPlanExposure> Ha = PlanMocks.GetMockPlanExposure("Ha", 10, 0, 180);
            Mock<IPlanExposure> OIII = PlanMocks.GetMockPlanExposure("OIII", 10, 0, 180);
            Mock<IPlanExposure> SII = PlanMocks.GetMockPlanExposure("SII", 10, 0, 180);
            Mock<IPlanExposure> L = PlanMocks.GetMockPlanExposure("L", 10, 0, 120);
            Mock<IPlanExposure> R = PlanMocks.GetMockPlanExposure("R", 10, 0, 120);
            Mock<IPlanExposure> G = PlanMocks.GetMockPlanExposure("G", 10, 0, 120);
            Mock<IPlanExposure> B = PlanMocks.GetMockPlanExposure("B", 10, 0, 120);

            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));
            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 3; i++) {
                AddActualExposures(actual, Ha.Object, 2);
                AddActualExposures(actual, OIII.Object, 2);
                AddActualExposures(actual, SII.Object, 2);
                actual.Add(new PlanDither());
            }

            AddActualExposures(actual, Ha.Object, 1);
            actual.Add(new PlanMessage(""));

            for (int i = 0; i < 2; i++) {
                AddActualExposures(actual, Ha.Object, 2);
                AddActualExposures(actual, OIII.Object, 2);
                AddActualExposures(actual, SII.Object, 2);
                actual.Add(new PlanDither());
                AddActualExposures(actual, L.Object, 1);
                AddActualExposures(actual, R.Object, 1);
                AddActualExposures(actual, G.Object, 1);
                AddActualExposures(actual, B.Object, 1);
                actual.Add(new PlanDither());
            }

            AddActualExposures(actual, Ha.Object, 1);
            AddActualExposures(actual, OIII.Object, 2);
            AddActualExposures(actual, SII.Object, 2);
            actual.Add(new PlanDither());

            for (int i = 0; i < 4; i++) {
                AddActualExposures(actual, L.Object, 1);
                AddActualExposures(actual, R.Object, 1);
                AddActualExposures(actual, G.Object, 1);
                AddActualExposures(actual, B.Object, 1);
                actual.Add(new PlanDither());
            }

            return actual;
        }

        private void AddActualExposures(List<IPlanInstruction> actual, IPlanExposure planFilter, int count, int dither = 0) {
            actual.Add(new PlanSwitchFilter(planFilter));
            actual.Add(new PlanSetReadoutMode(planFilter));
            for (int i = 0; i < count; i++) {
                actual.Add(new PlanTakeExposure(planFilter));
                if (dither > 0) {
                    if ((i + 1) % dither == 0) {
                        actual.Add(new PlanDither());
                    }
                }
            }
        }

        private void DumpInstructions(List<IPlanInstruction> list) {
            foreach (IPlanInstruction instruction in list) {
                TestContext.WriteLine(instruction);
            }

            TestContext.WriteLine();
        }
    }

    internal class TestNighttimeCircumstances : NighttimeCircumstances {

        public TestNighttimeCircumstances(DateTime civilTwilightStart,
                                          DateTime? nauticalTwilightStart,
                                          DateTime? astronomicalTwilightStart,
                                          DateTime? nighttimeStart,
                                          DateTime? nighttimeEnd,
                                          DateTime? astronomicalTwilightEnd,
                                          DateTime? nauticalTwilightEnd,
                                          DateTime civilTwilightEnd) {
            this.CivilTwilightStart = civilTwilightStart;
            this.NauticalTwilightStart = nauticalTwilightStart;
            this.AstronomicalTwilightStart = astronomicalTwilightStart;
            this.NighttimeStart = nighttimeStart;
            this.NighttimeEnd = nighttimeEnd;
            this.AstronomicalTwilightEnd = astronomicalTwilightEnd;
            this.NauticalTwilightEnd = nauticalTwilightEnd;
            this.CivilTwilightEnd = civilTwilightEnd;
        }

        public static TestNighttimeCircumstances GetNormal(DateTime dateTime) {
            return new TestNighttimeCircumstances(
                            dateTime.Date.AddHours(18),
                            dateTime.Date.AddHours(19),
                            dateTime.Date.AddHours(20),
                            dateTime.Date.AddHours(21),
                            dateTime.Date.AddDays(1).AddHours(4),
                            dateTime.Date.AddDays(1).AddHours(5),
                            dateTime.Date.AddDays(1).AddHours(6),
                            dateTime.Date.AddDays(1).AddHours(7));
        }

        public static TestNighttimeCircumstances GetNoNight(DateTime dateTime) {
            return new TestNighttimeCircumstances(
                            dateTime.Date.AddHours(18),
                            dateTime.Date.AddHours(19),
                            dateTime.Date.AddHours(20),
                            null, null,
                            dateTime.Date.AddDays(1).AddHours(5),
                            dateTime.Date.AddDays(1).AddHours(6),
                            dateTime.Date.AddDays(1).AddHours(7));
        }

        public static TestNighttimeCircumstances GetNoAstronomical(DateTime dateTime) {
            return new TestNighttimeCircumstances(
                            dateTime.Date.AddHours(18),
                            dateTime.Date.AddHours(19),
                            null, null,
                            null, null,
                            dateTime.Date.AddDays(1).AddHours(6),
                            dateTime.Date.AddDays(1).AddHours(7));
        }

        public static TestNighttimeCircumstances GetNoNautical(DateTime dateTime) {
            return new TestNighttimeCircumstances(
                            dateTime.Date.AddHours(18),
                            null, null,
                            null, null,
                            null, null,
                            dateTime.Date.AddDays(1).AddHours(7));
        }
    }
}