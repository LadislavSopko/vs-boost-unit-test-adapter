﻿// (C) Copyright 2015 ETAS GmbH (http://www.etas.com/)
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using BoostTestAdapter.Boost.Results;
using BoostTestAdapter.Boost.Results.LogEntryTypes;
using BoostTestAdapter.Utility;
using BoostTestAdapterNunit.Utility;
using NUnit.Framework;
using BoostTestResult = BoostTestAdapter.Boost.Results.TestResult;

namespace BoostTestAdapterNunit
{
    [TestFixture]
    internal class BoostTestResultTest
    {
        #region Test Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            this.TestResultCollection = new Dictionary<string, BoostTestResult>();
        }

        #endregion Test Setup/Teardown

        #region Test Data

        private IDictionary<string, BoostTestResult> TestResultCollection { get; set; }

        #endregion Test Data

        #region Helper Methods

        /// <summary>
        /// Asserts BoostTestResult against the expected details
        /// </summary>
        /// <param name="testResult">The BoostTestResult to test</param>
        /// <param name="parentTestResult">The expected parent BoostTestResult of testResult</param>
        /// <param name="name">The expected TestCase display name</param>
        /// <param name="result">The expected TestCase execution result</param>
        /// <param name="assertionsPassed">The expected number of passed assertions (e.g. BOOST_CHECKS)</param>
        /// <param name="assertionsFailed">The expected number of failed assertions (e.g. BOOST_CHECKS, BOOST_REQUIRE, BOOST_FAIL etc.)</param>
        /// <param name="expectedFailures">The expected number of expected test failures</param>
        private void AssertReportDetails(
            BoostTestResult testResult,
            BoostTestResult parentTestResult,
            string name,
            TestResultType result,
            uint assertionsPassed,
            uint assertionsFailed,
            uint expectedFailures
            )
        {
            Assert.That(testResult.Unit.Name, Is.EqualTo(name));

            if (parentTestResult == null)
            {
                Assert.That(testResult.Unit.Parent, Is.Null);
            }
            else
            {
                Assert.That(parentTestResult.Unit, Is.EqualTo(testResult.Unit.Parent));
            }

            Assert.That(testResult.Result, Is.EqualTo(result));

            Assert.That(testResult.AssertionsPassed, Is.EqualTo(assertionsPassed));
            Assert.That(testResult.AssertionsFailed, Is.EqualTo(assertionsFailed));
            Assert.That(testResult.ExpectedFailures, Is.EqualTo(expectedFailures));
        }

        /// <summary>
        /// Asserts BoostTestResult against the expected details
        /// </summary>
        /// <param name="testResult">The BoostTestResult to test</param>
        /// <param name="parentTestResult">The expected parent BoostTestResult of testResult</param>
        /// <param name="name">The expected TestCase display name</param>
        /// <param name="result">The expected TestCase execution result</param>
        /// <param name="assertionsPassed">The expected number of passed assertions (e.g. BOOST_CHECKS)</param>
        /// <param name="assertionsFailed">The expected number of failed assertions (e.g. BOOST_CHECKS, BOOST_REQUIRE, BOOST_FAIL etc.)</param>
        /// <param name="expectedFailures">The expected number of expected test failures</param>
        /// <param name="testCasesPassed">The expected number of passed child TestCases</param>
        /// <param name="testCasesFailed">The expected number of failed child TestCases</param>
        /// <param name="testCasesSkipped">The expected number of skipped child TestCases</param>
        /// <param name="testCasesAborted">The expected number of aborted child TestCases</param>
        private void AssertReportDetails(
            BoostTestResult testResult,
            BoostTestResult parentTestResult,
            string name,
            TestResultType result,
            uint assertionsPassed,
            uint assertionsFailed,
            uint expectedFailures,
            uint testCasesPassed,
            uint testCasesFailed,
            uint testCasesSkipped,
            uint testCasesAborted
            )
        {
            AssertReportDetails(testResult, parentTestResult, name, result, assertionsPassed, assertionsFailed,
                expectedFailures);

            Assert.That(testResult.TestCasesPassed, Is.EqualTo(testCasesPassed));
            Assert.That(testResult.TestCasesFailed, Is.EqualTo(testCasesFailed));
            Assert.That(testResult.TestCasesSkipped, Is.EqualTo(testCasesSkipped));
            Assert.That(testResult.TestCasesAborted, Is.EqualTo(testCasesAborted));
        }

        /// <summary>
        /// Asserts general log details contained within a BoostTestResult
        /// </summary>
        /// <param name="testResult">The BoostTestResult to test</param>
        /// <param name="duration">The expected test case execution duration</param>
        private void AssertLogDetails(BoostTestResult testResult, uint duration)
        {
            AssertLogDetails(testResult, duration, new List<LogEntry>());
        }

        /// <summary>
        /// Asserts general log details contained within a BoostTestResult
        /// </summary>
        /// <param name="testResult">The BoostTestResult to test</param>
        /// <param name="duration">The expected test case execution duration</param>
        /// <param name="entries">The expected list of log entries generated from test case execution</param>
        private void AssertLogDetails(BoostTestResult testResult, uint duration, IList<LogEntry> entries)
        {
            Assert.That(testResult.LogEntries.Count, Is.EqualTo(entries.Count));

            Assert.That(testResult.Duration, Is.EqualTo(duration));

            var logEntries = testResult.LogEntries.AsEnumerable();

            foreach (LogEntry entry in entries)
            {
                LogEntry found = Locate(entry, logEntries);
                Assert.That(found, Is.Not.Null);
                
                AssertSourceInfoDetails(found.Source, entry.Source);

                // Remove processed entries
                logEntries = logEntries.Where(x => x != found);
            }

            AssertErrorDetails(testResult, entries);
            AssertMemoryLeakDetails(testResult, entries);
        }

        /// <summary>
        /// Locates a similar log entry within the provided test result collection
        /// </summary>
        /// <param name="entry">The entry to locate within testResult</param>
        /// <param name="logEntries">The collection of log entries from which to look into</param>
        /// <returns>A LogEntry instance matching the provided entry or null if one cannot be found</returns>
        private LogEntry Locate(LogEntry entry, IEnumerable<LogEntry> logEntries)
        {
            return logEntries.FirstOrDefault(
                e =>
                {
                    // Truncate insignificant whitespace
                    var entryDetail = Regex.Replace(entry.Detail, @"\r|\n\s+", string.Empty);
                    var eDetail = Regex.Replace(e.Detail, @"\r|\n\s+", string.Empty);
                    return (e.ToString() == entry.ToString()) && (eDetail == entryDetail);
                });
        }

        /// <summary>
        /// Iterates over both collections in parallel
        /// </summary>
        /// <typeparam name="T">The type to filter out</typeparam>
        /// <param name="testResult">The lhs collection</param>
        /// <param name="entries">The rhs collection</param>
        /// <returns>An enumeration of a parallel iteration over both collections</returns>
        static private IEnumerable<Tuple<T, T>> DualIterate<T>(IEnumerable<T> lhs, IEnumerable<T> rhs)
        {
            var a = lhs.OfType<T>().GetEnumerator();
            var b = rhs.OfType<T>().GetEnumerator();

            while (a.MoveNext() && b.MoveNext())
            {
                yield return Tuple.Create(a.Current, b.Current);
            }

            while (a.MoveNext())
            {
                yield return Tuple.Create<T, T>(a.Current, default(T));
            }

            while (b.MoveNext())
            {
                yield return Tuple.Create<T, T>(default(T), b.Current);
            }
        }

        /// <summary>
        /// Helper wrapper over DualIterate specific for our test scenarios
        /// </summary>
        /// <typeparam name="T">Type to filter out</typeparam>
        /// <param name="testResult">The test result collection</param>
        /// <param name="entries">The entries collection</param>
        /// <returns>An enumeration of a parallel iteration over both collections</returns>
        private IEnumerable<Tuple<T, T>> DualIterate<T>(BoostTestResult testResult, IEnumerable<LogEntry> entries)
        {
            return DualIterate<T>(testResult.LogEntries.OfType<T>(), entries.OfType<T>());
        }

        /// <summary>
        /// Compares the error entries contained within the test result collection and the provided log entries
        /// </summary>
        /// <param name="testResult">The test result collection</param>
        /// <param name="entries">The log entries to compare against</param>
        private void AssertErrorDetails(BoostTestResult testResult, IEnumerable<LogEntry> entries)
        {
            foreach (var entry in DualIterate<LogEntryError>(testResult, entries))
            {
                Assert.That(entry.Item1, Is.Not.Null);
                Assert.That(entry.Item2, Is.Not.Null);

                Assert.That(entry.Item1.ContextFrames, Is.EquivalentTo(entry.Item2.ContextFrames));
            }
        }

        /// <summary>
        /// Compares the memory leak entries contained within the test result collection and the provided log entries
        /// </summary>
        /// <param name="testResult">The test result collection</param>
        /// <param name="entries">The log entries to compare against</param>
        private void AssertMemoryLeakDetails(BoostTestResult testResult, IEnumerable<LogEntry> entries)
        {
            foreach (var entry in DualIterate<LogEntryMemoryLeak>(testResult, entries))
            {
                Assert.That(entry.Item1, Is.Not.Null);
                Assert.That(entry.Item2, Is.Not.Null);

                AssertMemoryLeakDetails(entry.Item1, entry.Item2);
            }
        }

        /// <summary>
        /// Compares 2 LogEntryMemoryLeak for equivalence. Issues an assertion failure if leak information is not equivalent.
        /// </summary>
        /// <param name="lhs">The left-hand side LogEntryMemoryLeak instance</param>
        /// <param name="rhs">The right-hand side LogEntryMemoryLeak instance</param>
        private void AssertMemoryLeakDetails(LogEntryMemoryLeak lhs, LogEntryMemoryLeak rhs)
        {
            AssertSourceInfoDetails(lhs.Source, rhs.Source);
            Assert.AreEqual(lhs.LeakLeakedDataContents, rhs.LeakLeakedDataContents);
            Assert.AreEqual(lhs.LeakMemoryAllocationNumber, rhs.LeakMemoryAllocationNumber);
            Assert.AreEqual(lhs.LeakSizeInBytes, rhs.LeakSizeInBytes);
        }

        /// <summary>
        /// Tests the provided LogEntryException's properties against the expected values
        /// </summary>
        /// <param name="entry">The LogEntryException to test</param>
        /// <param name="checkpointInfo">The expected source file information for the exception</param>
        /// <param name="checkpointMessage">The expected checkpoint message</param>
        private void AssertLogEntryExceptionDetails(LogEntryException entry, SourceFileInfo checkpointInfo,
            string checkpointMessage)
        {
            AssertSourceInfoDetails(checkpointInfo, entry.LastCheckpoint);
            Assert.That(entry.CheckpointDetail, Is.EqualTo(checkpointMessage));
        }

        /// <summary>
        /// Tests the provided LogEntry's general properties against the expected values
        /// </summary>
        /// <param name="entry">The LogEntryException to test</param>
        /// <param name="entryType">The expected log entry type</param>
        /// <param name="message">The expected log message</param>
        /// <param name="info">The expected source file information for the log entry</param>
        private void AssertLogEntryDetails(LogEntry entry, string entryType, string message, SourceFileInfo info)
        {
            Assert.That(entry.ToString(), Is.EqualTo(entryType));
            Assert.That(entry.Detail, Is.EqualTo(message));

            AssertSourceInfoDetails(entry.Source, info);
        }

        /// <summary>
        /// Compares 2 SourceFileInfo for equivalence. Issues an assertion failure if leak information is not equivalent.
        /// </summary>
        /// <param name="lhs">The left-hand side SourceFileInfo instance</param>
        /// <param name="rhs">The right-hand side SourceFileInfo instance</param>
        private void AssertSourceInfoDetails(SourceFileInfo lhs, SourceFileInfo rhs)
        {
            if (lhs == null)
            {
                Assert.That(rhs, Is.Null);
            }
            else
            {
                Assert.That(lhs.File, Is.EqualTo(rhs.File));
                Assert.That(lhs.LineNumber, Is.EqualTo(rhs.LineNumber));
            }
        }

        /// <summary>
        /// Tests test result collection for the expected contents when populated from 'PassedTest.sample.test.report.xml'
        /// </summary>
        /// <returns>The BoostTestResult for the sole test case present in the 'PassedTest.sample.test.report.xml' report</returns>
        private BoostTestResult AssertPassedReportDetails()
        {
            BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
            Assert.That(masterSuiteResult, Is.Not.Null);

            AssertReportDetails(masterSuiteResult, null, "CnvrtTest", TestResultType.Passed, 3, 0, 0, 1, 0, 0, 0);

            BoostTestResult testSuiteResult = this.TestResultCollection["XmlDomInterfaceTestSuite"];
            Assert.That(testSuiteResult, Is.Not.Null);

            AssertReportDetails(testSuiteResult, masterSuiteResult, "XmlDomInterfaceTestSuite", TestResultType.Passed, 3,
                0, 0, 1, 0, 0, 0);

            BoostTestResult testCaseResult =
                this.TestResultCollection["XmlDomInterfaceTestSuite/ParseXmlFileWithoutValidationTest"];
            Assert.That(testCaseResult, Is.Not.Null);

            AssertReportDetails(testCaseResult, testSuiteResult, "ParseXmlFileWithoutValidationTest",
                TestResultType.Passed, 3, 0, 0);

            return testCaseResult;
        }
        
        /// <summary>
        /// Parses the *Xml* report stream and the *Xml* log stream into the contained test result collection.
        /// Additionally, the standard output and standard error can also be parsed.
        /// </summary>
        /// <param name="report">the path of the XML file report to parse.</param>
        /// <param name="log">the path of the log file that is to be parsed</param>
        /// <param name="stdout">The text standard output stream to parse.</param>
        /// <param name="stderr">The text standard error stream to parse.</param>
        private IDictionary<string, BoostTestResult> Parse(Stream report, Stream log, Stream stdout = null, Stream stderr = null)
        {
            var collection = this.TestResultCollection /* new Dictionary<string, BoostTestResult>() */;

            Parse(new BoostXmlReport(collection), report);
            Parse(new BoostXmlLog(collection), log);
            Parse(new BoostStandardOutput(collection) { FailTestOnMemoryLeak = true }, stdout);
            Parse(new BoostStandardError(collection) { FailTestOnMemoryLeak = true }, stderr);

            return collection;
        }

        private static IDictionary<string, BoostTestResult> Parse(IBoostTestResultParser parser, Stream content)
        {
            var enc = Encoding.GetEncoding("iso-8859-1");
            enc = (Encoding) enc.Clone();
            enc.EncoderFallback = new EncoderReplacementFallback(string.Empty);

            using (var reader = new StreamReader(content ?? Stream.Null, enc))
            {
                return parser.Parse(reader.ReadToEnd());
            }
        }
        
        /// <summary>
        /// Constructs a LogEntry and populates the main components
        /// </summary>
        /// <typeparam name="T">A derived LogEntry class type</typeparam>
        /// <param name="detail">The detail message. 'null' to use the default.</param>
        /// <param name="source">The source file information assocaited to the log entry. 'null' to use the default.</param>
        /// <param name="context">The log entry context. 'null' to use the default.</param>
        /// <returns>A new LogEntry-derived instance populated accordingly</returns>
        private T MakeLogEntry<T>(string detail = null, SourceFileInfo source = null, IEnumerable<string> context = null) where T : LogEntry, new()
        {
            return LogEntry.MakeLogEntry<T>(detail, source, context);
        }

        /// <summary>
        /// Constructs a LogEntryMemoryLeak and populates the main components
        /// </summary>
        /// <param name="leakLocation">The location of the memory leak.</param>
        /// <param name="leakSizeInBytes">The number of bytes leaked.</param>
        /// <param name="leakMemoryAllocationNumber">The memory allocation number.</param>
        /// <param name="leakLeakedDataContents">The memory contents which were leaked.</param>
        /// <returns>A new LogEntryMemoryLeak instance populated accordingly</returns>
        private LogEntryMemoryLeak MakeLogEntryMemoryLeak(SourceFileInfo leakLocation, uint? leakSizeInBytes, uint? leakMemoryAllocationNumber, string leakLeakedDataContents)
        {
            return LogEntryMemoryLeak.MakeLogEntryMemoryLeak(leakLocation, leakSizeInBytes, leakMemoryAllocationNumber, leakLeakedDataContents);
        }

        #endregion Helper Methods

        /// <summary>
        /// Boost Test XML log containing special characters.
        /// </summary>
        /// Test aims:
        ///         - The aim of the test is to make sure that we are able to handle Boost xml logs that contain special characters. Boost UTF does
        /// not technically generate a valid xml document so we need to add the encoding declaration ourselves (this is done in class BoostTestXMLOutput) 
        [Test]
        public void ParseBoostReportLogContainingGermanCharacters()
        {
            using (var report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.SpecialCharacters.sample.test.report.xml"))
            using (var log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.SpecialCharacters.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "MyTest", TestResultType.Failed, 0, 4, 0, 0, 1, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["SpecialCharactersInStringAndIdentifier"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "SpecialCharactersInStringAndIdentifier", TestResultType.Failed, 0, 4, 0);
                AssertLogDetails(testCaseResult
                    , 2000
                    , new[]
                    {
                    MakeLogEntry<LogEntryError>("check germanSpecialCharacterString == \"NotTheSameString\" failed [Hello my name is Rüdiger != NotTheSameString]",new SourceFileInfo("boostunittest.cpp", 8)),
                    MakeLogEntry<LogEntryError>("check germanSpecialCharacterString == \"\" failed [üöä != ]",new SourceFileInfo("boostunittest.cpp", 12)),
                    MakeLogEntry<LogEntryError>("check anzahlDerÄnderungen == 1 failed [2 != 1]",new SourceFileInfo("boostunittest.cpp", 17)),
                    MakeLogEntry<LogEntryError>("check üöä == 1 failed [2 != 1]",new SourceFileInfo("boostunittest.cpp", 18)),
                    });

                Assert.That(testCaseResult.LogEntries.Count, Is.EqualTo(4));
            }
        }

        /// <summary>
        /// Boost Test XML log containing control characters.
        /// </summary>
        /// Test aims:
        ///         - The aim of the test is to make sure that we are able to handle Boost xml logs that contain invalid or control characters. Boost UTF does
        /// not technically generate a valid XML document so we need to add the encoding declaration ourselves (this is done in class BoostTestXMLOutput).
        /// We also filter out all the NUL characters and replace all the control (less than 32) characters in the CDATA section with the hexadecimal representation.
        [Test]
        public void ParseBoostReportLogContainingControlCharacters()
        {
            using (var report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.ControlCharacters.sample.test.report.xml"))
            using (var log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.ControlCharacters.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "ControlCharactersUnitTests", TestResultType.Failed, 0, 1, 0, 0, 1, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["TestControlChar"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "TestControlChar", TestResultType.Failed, 0, 1, 0);
                AssertLogDetails(testCaseResult
                    , 2000
                    , new[]
                    {
                    MakeLogEntry<LogEntryError>("check { vect1.cbegin(), vect1.cend() } == { vect2.cbegin(), vect2.cend() } failed. \nMismatch in a position 1: 0x00 != 0x01\nMismatch in a position 2: 0x01 != 0x02\nMismatch in a position 3: 0x03 != 0x04\nMismatch in a position 7: 0x00 != A\nCollections size mismatch: 32 != 31",new SourceFileInfo("boostunittest.cpp", 8)),
                });

                Assert.That(testCaseResult.LogEntries.Count, Is.EqualTo(1));
            }
        }



        /// <summary>
        /// Boost Test Xml report + log detailing an exception.
        /// 
        /// Test aims:
        ///     - Boost Test results for a test case execution resulting in an exception are parsed accordingly.
        /// </summary>
        [Test]
        public void ParseExceptionThrownReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.AbortedTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.AbortedTest.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "MyTest", TestResultType.Failed, 0, 1, 0, 0, 1, 0, 1);

                BoostTestResult testCaseResult = this.TestResultCollection["BoostUnitTest123"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "BoostUnitTest123", TestResultType.Aborted, 0, 1, 0);
                AssertLogDetails(testCaseResult, 0, new[] { MakeLogEntry<LogEntryException>("C string: some error", new SourceFileInfo("unknown location", 0)) });

                Assert.That(testCaseResult.LogEntries.Count, Is.EqualTo(1));

                LogEntry entry = testCaseResult.LogEntries.First();
                AssertLogEntryDetails(entry, "Exception", "C string: some error", new SourceFileInfo("unknown location", 0));
                AssertLogEntryExceptionDetails((LogEntryException)entry, new SourceFileInfo("boostunittestsample.cpp", 13), "Going to throw an exception");
            }
        }

        /// <summary>
        /// Boost Test Xml report + log detailing a test case failure due to BOOST_REQUIRE.
        /// 
        /// Test aims:
        ///     - Boost Test results for a test case execution resulting in failure are parsed accordingly.
        /// </summary>
        [Test]
        public void ParseRequireFailedReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.FailedRequireTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.FailedRequireTest.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "Test runner test", TestResultType.Failed, 0, 2, 0, 0, 1, 0, 1);

                BoostTestResult testCaseResult = this.TestResultCollection["test1"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "test1", TestResultType.Aborted, 0, 2, 0);
                AssertLogDetails(testCaseResult, 0, new LogEntry[] {
                    MakeLogEntry<LogEntryError>("check i == 2 failed [0 != 2]", new SourceFileInfo("test_runner_test.cpp", 26)),
                    MakeLogEntry<LogEntryFatalError>("critical check i == 2 failed [0 != 2]", new SourceFileInfo("test_runner_test.cpp", 28)),
                });
            }
        }

        /// <summary>
        /// Boost Test Xml report + log detailing a test case failure due to BOOST_CHECK.
        /// 
        /// Test aims:
        ///     - Boost Test results for a test case execution resulting in failure are parsed accordingly.
        /// </summary>
        [Test]
        public void ParseBoostFailReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.BoostFailTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.BoostFailTest.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "Test runner test", TestResultType.Failed, 0, 1, 0, 0, 1, 0, 1);

                BoostTestResult testCaseResult = this.TestResultCollection["test3"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "test3", TestResultType.Aborted, 0, 1, 0);

                Assert.That(testCaseResult.LogEntries.Count, Is.EqualTo(1));

                LogEntry entry = testCaseResult.LogEntries.First();
                AssertLogDetails(testCaseResult, 1000, new LogEntry[] {
                    MakeLogEntry<LogEntryFatalError>("Failure", new SourceFileInfo("test_runner_test.cpp", 93)),
                });
            }
        }

        /// <summary>
        /// Boost Test Xml report + log detailing a passed test case.
        /// 
        /// Test aims:
        ///     - Boost Test results for a positive test case execution are parsed accordingly.
        /// </summary>
        [Test]
        public void ParsePassedReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.PassedTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.PassedTest.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult testCaseResult = AssertPassedReportDetails();
                AssertLogDetails(testCaseResult, 18457000);
            }
        }

        /// <summary>
        /// Boost Test Xml report (only) detailing a passed test case.
        /// 
        /// Test aims:
        ///     - Boost Test results for a positive test case execution are parsed accordingly.
        ///     - Boost Test results can be built from an Xml report without the need of the Xml log.
        /// </summary>
        [Test]
        public void ParsePassedReportOnly()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.PassedTest.sample.test.report.xml"))
            {
                Parse(report, null);

                BoostTestResult testCaseResult = AssertPassedReportDetails();
                AssertLogDetails(testCaseResult, 0);
            }
        }

        /// <summary>
        /// Boost Test Xml report + log detailing a passed test case nested within test suites.
        /// 
        /// Test aims:
        ///     - Boost Test results for a positive test case execution are parsed accordingly.
        /// </summary>
        [Test]
        public void ParseNestedTestSuiteReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.NestedTestSuite.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.NestedTestSuite.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "Test runner test", TestResultType.Passed, 2, 0, 0, 1, 0, 0, 0);

                BoostTestResult testSuiteResult = this.TestResultCollection["SampleSuite"];
                Assert.That(testSuiteResult, Is.Not.Null);

                AssertReportDetails(testSuiteResult, masterSuiteResult, "SampleSuite", TestResultType.Passed, 2, 0, 0, 1, 0, 0, 0);

                BoostTestResult nestedTestSuiteResult = this.TestResultCollection["SampleSuite/SampleNestedSuite"];
                Assert.That(nestedTestSuiteResult, Is.Not.Null);

                AssertReportDetails(nestedTestSuiteResult, testSuiteResult, "SampleNestedSuite", TestResultType.Passed, 2, 0, 0, 1, 0, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["SampleSuite/SampleNestedSuite/test3"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, nestedTestSuiteResult, "test3", TestResultType.Passed, 2, 0, 0);
                AssertLogDetails(testCaseResult, 1000, new LogEntry[] {
                    MakeLogEntry<LogEntryMessage>("Message from test3", new SourceFileInfo("test_runner_test.cpp", 48)),
                    MakeLogEntry<LogEntryWarning>("condition false == true is not satisfied", new SourceFileInfo("test_runner_test.cpp", 50)), 
                });
            }
        }

        /// <summary>
        /// Boost Test Xml report + log detailing multiple passed test cases.
        /// 
        /// Test aims:
        ///     - Boost Test results for positive test case execution are parsed accordingly.
        /// </summary>
        [Test]
        public void ParseMultipleTestResultsReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MultipleTests.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MultipleTests.sample.test.log.xml"))
            {
                Parse(report, log);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "CnvrtTest", TestResultType.Passed, 50, 0, 0, 4, 0, 0, 0);

                BoostTestResult testSuiteResult = this.TestResultCollection["CCRootSerialiserTestSuite"];
                Assert.That(testSuiteResult, Is.Not.Null);

                AssertReportDetails(testSuiteResult, masterSuiteResult, "CCRootSerialiserTestSuite", TestResultType.Passed, 2, 0, 0, 2, 0, 0, 0);

                ICollection<BoostTestResult> results = new HashSet<BoostTestResult>();

                {
                    BoostTestResult testCaseResult = this.TestResultCollection["CCRootSerialiserTestSuite/DeserialiseInvalidFile"];
                    Assert.That(testCaseResult, Is.Not.Null);

                    AssertReportDetails(testCaseResult, testSuiteResult, "DeserialiseInvalidFile", TestResultType.Passed, 1, 0, 0);
                    AssertLogDetails(testCaseResult, 5000);

                    results.Add(testCaseResult);

                    BoostTestResult testCase2Result = this.TestResultCollection["CCRootSerialiserTestSuite/DeserialiseNonExistingFile"];
                    Assert.That(testCase2Result, Is.Not.Null);

                    AssertReportDetails(testCase2Result, testSuiteResult, "DeserialiseNonExistingFile", TestResultType.Passed, 1, 0, 0);
                    AssertLogDetails(testCase2Result, 0);

                    results.Add(testCase2Result);
                }

                BoostTestResult testSuite2Result = this.TestResultCollection["DET108781TestSuite"];
                Assert.That(testSuite2Result, Is.Not.Null);

                AssertReportDetails(testSuite2Result, masterSuiteResult, "DET108781TestSuite", TestResultType.Passed, 48, 0, 0, 2, 0, 0, 0);

                {
                    BoostTestResult testCaseResult = this.TestResultCollection["DET108781TestSuite/ExtendedMultiplexingTest"];
                    Assert.That(testCaseResult, Is.Not.Null);

                    AssertReportDetails(testCaseResult, testSuite2Result, "ExtendedMultiplexingTest", TestResultType.Passed, 26, 0, 0);
                    AssertLogDetails(testCaseResult, 792000);

                    results.Add(testCaseResult);

                    BoostTestResult testCase2Result = this.TestResultCollection["DET108781TestSuite/QPGroupingTest"];
                    Assert.That(testCase2Result, Is.Not.Null);

                    AssertReportDetails(testCase2Result, testSuite2Result, "QPGroupingTest", TestResultType.Passed, 22, 0, 0);
                    AssertLogDetails(testCase2Result, 941000);

                    results.Add(testCase2Result);
                }

                // Only *TestCase* results should be enumerated.
                Assert.That(results.Intersect(this.TestResultCollection.Values).Count(), Is.EqualTo(results.Count));
            }
        }

        /// <summary>
        /// Boost Test ''Xml'' report detailing that no tests could be found with the given name.
        /// 
        /// Test aims:
        ///     - Boost Test results in a non-Xml format throw a parse exception.
        /// </summary>
        [Test]
        [ExpectedException(typeof(XmlException))]
        public void ParseNoMatchingTestsReportLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.NoMatchingTests.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.NoMatchingTests.sample.test.log.xml"))
            {
                Parse(report, log);
            }
        }

        /// <summary>
        /// Tests the correct memory leak discovery for an output that does not contain the source file and the line numbers available
        /// </summary>
        [Test]
        public void MemoryLeakNoSourceFileAndLineNumbersAvailable()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.log.xml"))
            using (Stream stdout = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.NoSourceFileAndLineNumbersAvailable.test.stdout.log"))
            {
                Parse(report, log, stdout);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                // NOTE The values here do not match the Xml report file since the attributes:
                //      'test_cases_passed', 'test_cases_failed', 'test_cases_skipped' and 'test_cases_aborted'
                //      are computed in case test results are modified from the Xml output
                //      (which in the case of memory leaks, passing tests may be changed to failed).
                AssertReportDetails(masterSuiteResult, null, "MyTest", TestResultType.Failed, 0, 0, 0, 0, 1, 0, 0);

                BoostTestResult testSuiteResult = this.TestResultCollection["LeakingSuite"];
                Assert.That(testSuiteResult, Is.Not.Null);

                AssertReportDetails(testSuiteResult, masterSuiteResult, "LeakingSuite", TestResultType.Failed, 0, 0, 0, 0, 1, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["LeakingSuite/LeakingTestCase"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, testSuiteResult, "LeakingTestCase", TestResultType.Failed, 0, 0, 0);
                AssertLogDetails(testCaseResult, 0, new LogEntry[] {
                    MakeLogEntry<LogEntryMessage>("Test case LeakingTestCase did not check any assertions", new SourceFileInfo("./boost/test/impl/results_collector.ipp", 220)),
                    MakeLogEntryMemoryLeak(null, 8, 837, " Data: <        > 98 D5 D9 00 00 00 00 00 \n"),
                    MakeLogEntryMemoryLeak(null, 32, 836, " Data: <`-  Leak...     > 60 2D BD 00 4C 65 61 6B 2E 2E 2E 00 CD CD CD CD ")
                });
            }
        }

        /// <summary>
        /// Tests the correct leak discovery for an output that does contain the source file and the line numbers available
        /// </summary>
        [Test]
        public void MemoryLeakSourceFileAndLineNumbersAvailable()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.log.xml"))
            using (Stream stdout = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.SourceFileAndLineNumbersAvailable.test.stdout.log"))
            {
                Assert.IsNotNull(report);
                Assert.IsNotNull(log);
                Assert.IsNotNull(stdout);

                Parse(report, log, stdout);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                // NOTE The values here do not match the Xml report file since the attributes:
                //      'test_cases_passed', 'test_cases_failed', 'test_cases_skipped' and 'test_cases_aborted'
                //      are computed in case test results are modified from the Xml output
                //      (which in the case of memory leaks, passing tests may be changed to failed).
                AssertReportDetails(masterSuiteResult, null, "MyTest", TestResultType.Failed, 0, 0, 0, 0, 1, 0, 0);

                BoostTestResult testSuiteResult = this.TestResultCollection["LeakingSuite"];
                Assert.That(testSuiteResult, Is.Not.Null);

                AssertReportDetails(testSuiteResult, masterSuiteResult, "LeakingSuite", TestResultType.Failed, 0, 0, 0, 0, 1, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["LeakingSuite/LeakingTestCase"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, testSuiteResult, "LeakingTestCase", TestResultType.Failed, 0, 0, 0);
                AssertLogDetails(testCaseResult, 0, new LogEntry[] {
                    MakeLogEntry<LogEntryMessage>("Test case LeakingTestCase did not check any assertions", new SourceFileInfo("./boost/test/impl/results_collector.ipp", 220)),
                    MakeLogEntryMemoryLeak(new SourceFileInfo(@"d:\hwa\dev\svn\boostunittestadapterdev\branches\tempbugfixing\sample\boostunittest\boostunittest2\adapterbugs.cpp", 60), 4, 935, " Data: <    > CD CD CD CD \n"),
                    MakeLogEntryMemoryLeak(new SourceFileInfo(@"d:\hwa\dev\svn\boostunittestadapterdev\branches\tempbugfixing\sample\boostunittest\boostunittest2\adapterbugs.cpp", 57), 4, 934, " Data: <    > F5 01 00 00 ")
                });
            }
        }

        /// <summary>
        /// Boost Test Xml report + standard out and standard error detailing positive test execution.
        /// 
        /// Test aims:
        ///     - Boost Test results in can parse and record standard output and standard error text output.
        /// </summary>
        [Test]
        public void ParseOutputTestReportLogStdOutStdErr()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.OutputTest.sample.test.report.xml"))
            using (Stream stdout = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.OutputTest.sample.test.stdout.log"))
            using (Stream stderr = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.OutputTest.sample.test.stderr.log"))
            {
                Parse(report, null, stdout, stderr);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "MyTest", TestResultType.Passed, 1, 0, 0, 1, 0, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["MyTestCase"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "MyTestCase", TestResultType.Passed, 1, 0, 0);
                AssertLogDetails(testCaseResult, 0, new LogEntry[] {
                    MakeLogEntry<LogEntryStandardOutputMessage>("Hello Standard Output World"),
                    MakeLogEntry<LogEntryStandardErrorMessage>("Hello Standard Error World")
                });
            }
        }

        /// <summary>
        /// Boost Test Xml log of a test fixture using 'depends_on' which generates invalid Xml.
        /// 
        /// See also:
        /// - https://github.com/etas/vs-boost-unit-test-adapter/pull/72
        /// - https://github.com/etas/vs-boost-unit-test-adapter/pull/77
        /// 
        /// Test aims:
        ///     - Boost Test results can handle invalid Xml logs and extract and interpret the <TestLog> portion if available.
        /// </summary>
        [Test]
        public void ParseInvalidXmlLog()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.InvalidXmlLog.test_hlp.exe.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.InvalidXmlLog.test_hlp.exe.test.log.xml"))
            {
                Parse(report, log, null, null);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "test_hlp", TestResultType.Failed, 0, 3, 0, 1, 1, 0, 0);

                BoostTestResult testSuiteResult = this.TestResultCollection["bpts"];
                Assert.That(testSuiteResult, Is.Not.Null);

                AssertReportDetails(testSuiteResult, masterSuiteResult, "bpts", TestResultType.Failed, 0, 3, 0, 1, 1, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["bpts/plugin_fxs"];
                Assert.That(testCaseResult, Is.Not.Null);
                AssertReportDetails(testCaseResult, testSuiteResult, "plugin_fxs", TestResultType.Passed, 0, 0, 0);

                testCaseResult = this.TestResultCollection["bpts/thread_hwbpt"];
                Assert.That(testCaseResult, Is.Not.Null);
                AssertReportDetails(testCaseResult, testSuiteResult, "thread_hwbpt", TestResultType.Failed, 0, 3, 0);
            }
        }

        /// <summary>
        /// Boost Test Xml log of a test using contexts (i.e. BOOST_TEST_CONTEXT and BOOST_TEST_INFO)
        /// 
        /// Reference: http://www.boost.org/doc/libs/1_60_0/libs/test/doc/html/boost_test/test_output/contexts.html
        ///
        /// Test aims:
        ///     - Boost Test results can properly parse and represent context information
        /// </summary>
        [Test]
        public void ParseTestContext()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.TestContext.ExampleBoostUnittest.exe.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.TestContext.ExampleBoostUnittest.exe.test.log.xml"))
            {
                Parse(report, log, null, null);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                AssertReportDetails(masterSuiteResult, null, "mytest", TestResultType.Failed, 5, 4, 0, 0, 1, 0, 0);
                
                BoostTestResult testCaseResult = this.TestResultCollection["test1"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "test1", TestResultType.Failed, 5, 4, 0);
                AssertLogDetails(testCaseResult, 4000, new LogEntry[] {
                    MakeLogEntry<LogEntryError>(
                        "check processor.op2(i, j) has failed",
                        new SourceFileInfo("c:/exampleboostunittest/source.cpp", 19),
                        new string[] { "With optimization level 1", "With parameter i = 1", "With parameter j = 0" }
                    ),
                    MakeLogEntry<LogEntryError>(
                        "check processor.op1(i) has failed",
                        new SourceFileInfo("c:/exampleboostunittest/source.cpp", 16),
                        new string[] { "With optimization level 2", "With parameter i = 0" }
                    ),
                    MakeLogEntry<LogEntryError>(
                        "check processor.op1(i) has failed",
                        new SourceFileInfo("c:/exampleboostunittest/source.cpp", 16),
                        new string[] { "With optimization level 2", "With parameter i = 1" }
                    ),
                    MakeLogEntry<LogEntryError>(
                        "check processor.op2(i, j) has failed",
                        new SourceFileInfo("c:/exampleboostunittest/source.cpp", 19),
                        new string[] { "With optimization level 2", "With parameter i = 1", "With parameter j = 0" }
                    ),
                });
            }
        }

        /// <summary>
        /// Tests the correct memory leak discovery is also applied on standard error
        /// </summary>
        [Test]
        public void MemoryLeakStandardError()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.log.xml"))
            using (Stream stdout = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.Empty.sample.test.stdout.log"))
            using (Stream stderr = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.MemoryLeakTest.sample.test.stderr.log"))
            {
                Parse(report, log, stdout, stderr);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);

                // NOTE The values here do not match the Xml report file since the attributes:
                //      'test_cases_passed', 'test_cases_failed', 'test_cases_skipped' and 'test_cases_aborted'
                //      are computed in case test results are modified from the Xml output
                //      (which in the case of memory leaks, passing tests may be changed to failed).
                AssertReportDetails(masterSuiteResult, null, "MyTest", TestResultType.Failed, 0, 0, 0, 0, 1, 0, 0);

                BoostTestResult testSuiteResult = this.TestResultCollection["LeakingSuite"];
                Assert.That(testSuiteResult, Is.Not.Null);

                AssertReportDetails(testSuiteResult, masterSuiteResult, "LeakingSuite", TestResultType.Failed, 0, 0, 0, 0, 1, 0, 0);

                BoostTestResult testCaseResult = this.TestResultCollection["LeakingSuite/LeakingTestCase"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, testSuiteResult, "LeakingTestCase", TestResultType.Failed, 0, 0, 0);
                AssertLogDetails(testCaseResult, 0, new LogEntry[] {
                    MakeLogEntry<LogEntryMessage>("Test case LeakingTestCase did not check any assertions", new SourceFileInfo("./boost/test/impl/results_collector.ipp", 220)),
                    MakeLogEntryMemoryLeak(null, 8, 837, " Data: <        > 98 D5 D9 00 00 00 00 00 \n"),
                    MakeLogEntryMemoryLeak(null, 32, 836, " Data: <`-  Leak...     > 60 2D BD 00 4C 65 61 6B 2E 2E 2E 00 CD CD CD CD ")
                });
            }
        }

        /// <summary>
        /// Assert that multiple test reports for the same test are aggregated under the same result
        /// </summary>
        [Test]
        public void DataTestCaseResultAggregation()
        {
            using (Stream report = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.DataTestCase.sample.test.report.xml"))
            using (Stream log = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.DataTestCase.sample.test.log.xml"))
            using (Stream stdout = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.Empty.sample.test.stdout.log"))
            using (Stream stderr = TestHelper.LoadEmbeddedResource("BoostTestAdapterNunit.Resources.ReportsLogs.Empty.sample.test.stderr.log"))
            {
                Parse(report, log, stdout, stderr);

                BoostTestResult masterSuiteResult = this.TestResultCollection[string.Empty];
                Assert.That(masterSuiteResult, Is.Not.Null);
                
                // NOTE The values here do not match the Xml report file since the results are aggregated as one.
                //      All tests are considered as failed since they share the same result.
                AssertReportDetails(masterSuiteResult, null, "MultipleTests", TestResultType.Failed, 4, 1, 0, 0, 1, 0, 0);
                
                BoostTestResult testCaseResult = this.TestResultCollection["test_case_arity1_implicit"];
                Assert.That(testCaseResult, Is.Not.Null);

                AssertReportDetails(testCaseResult, masterSuiteResult, "test_case_arity1_implicit", TestResultType.Failed, 4, 1, 0);

                Assert.That(testCaseResult.Duration, Is.EqualTo(4000));
            }
        }
    }
}
