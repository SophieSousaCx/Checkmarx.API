﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Xml.Linq;
using Checkmarx.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Checkmarx.API.Tests
{
    [TestClass]
    public class CxClientUnitTests
    {
        private static CxClient client;
        private static CxClient clientV9;

        const string URL_V9 = "https://clientV9";
        const string PASS_V9 = "";
        const string URL_V89 = "https://clientV89";
        const string PASS_89 = "";

        const string USER = "";

        [ClassInitialize]
        public static void InitializeTest(TestContext testContext)
        {
            client =
                new CxClient(new Uri(URL_V89),
                USER,
                new NetworkCredential("", PASS_89).Password);

            clientV9 =
                new CxClient(new Uri(URL_V9),
                USER,
                new NetworkCredential("", PASS_V9).Password);
        }

        [TestMethod]
        public void TestGetVersion()
        {
            Assert.AreEqual("V 9.0", clientV9.Version);
        }

        [TestMethod]
        public void TestGetOSA_V9()
        {
            CxClient client = new CxClient(new Uri(URL_V9), USER, PASS_V9);

            var license = client.GetLicense();

            Assert.IsTrue(license.IsOsaEnabled);
        }


        [TestMethod]
        public void TestGetOSA_V8()
        {
            CxClient client = new CxClient(new Uri(URL_V89), USER, PASS_89);

            var license = client.GetLicense();

            Assert.IsTrue(license.IsOsaEnabled);
        }

        [TestMethod]
        public void V9ConnectTest()
        {
            CxClient client = new CxClient(new Uri(URL_V9), USER, PASS_V9);

            foreach (var item in client.GetProjects())
            {
                Trace.WriteLine(item.Key);
            }
        }

        [TestMethod]
        public void GetXMLReport()
        {
            // var memoryStream = client.GetScanReport(1010026, ReportType.XML);

            var memoryStream = @"csr-internal.xml";

            XDocument document = XDocument.Load(memoryStream);

            var queries = from item in document.Root.Elements("Query")
                          select new
                          {
                              CWE = int.Parse(item.Attribute("cweId").Value),
                              Name = item.Attribute("name").Value,
                              Language = item.Attribute("Language").Value,
                              Severity = item.Attribute("Severity").Value,
                              Results = item.Elements("Result")
                          };

            foreach (var query in queries)
            {
                var results = from result in query.Results
                              select new
                              {
                                  Id = long.Parse(result.Attribute("NodeId").Value),
                                  SimilarityId = result.Elements("Path").Single().Attribute("SimilarityId").Value,
                                  IsNew = result.Attribute("Status").Value == "New",
                                  State = result.Attribute("state").Value,
                                  Link = result.Attribute("DeepLink").Value,
                                  TruePositive = result.Attribute("FalsePositive").Value != "False",
                                  Comments = HttpUtility.HtmlDecode(result.Attribute("Remark").Value),
                                  AssignTo = result.Attribute("AssignToUser").Value,
                                  Nodes = result.Elements("Path").Single().Elements()
                              };

                Trace.WriteLine($"Query: {query.Language} {query.Name} - {query.Severity} CWE:{query.CWE} - {query.Results.Count()}");

                foreach (var result in results.Where(x => x.TruePositive))
                {
                    Trace.WriteLine($"\n\tResult: {result.Id} - Confirmed: {result.TruePositive} - {result.Nodes.Count()}\r\n{result.Comments}\n");

                    var nodes = from node in result.Nodes
                                select new
                                {
                                    FileName = node.Element("FileName").Value,
                                    Snippet = HttpUtility.HtmlDecode(node.Element("Snippet").Element("Code").Value),
                                };
                }
            }
        }

        [TestMethod]
        public void getRTFReport()
        {
            var memoryStream = client.GetScanReport(1010026, ReportType.RTF);
            string outputFile = @"report.rtf";
            using (FileStream fs = File.Create(outputFile))
            {
                memoryStream.CopyTo(fs);
            }
        }

        [TestMethod]
        public void getPDFReport()
        {
            var memoryStream = client.GetScanReport(1010026, ReportType.PDF);
            using (FileStream fs = File.Create(@"report.pdf"))
            {
                memoryStream.CopyTo(fs);
            }
        }

        [TestMethod]
        public void GetProjectsTest()
        {

            foreach (var keyValuePair in client.GetTeams())
            {
                Console.WriteLine(keyValuePair.Value);
            }

            foreach (var item in client.GetProjects())
            {
                Console.WriteLine($" === {item.Value} === ");

                var excluded = client.GetExcludedSettings(item.Key);
                Console.WriteLine("ExcludedFolders:" + excluded.Item1);
                Console.WriteLine("ExcludedFiles:" + excluded.Item2);
                Console.WriteLine("Preset:" + client.GetSASTPreset(item.Key));

                Console.WriteLine("== CxSAST Scans ==");
                foreach (var sastScan in client.GetSASTScans(item.Key))
                {
                    var result = client.GetSASTResults(sastScan);
                    //var scanState = sastScan["scanState"];

                    //result.LoC = (int)scanState.SelectToken("linesOfCode");
                    //result.FailedLoC = (int)scanState.SelectToken("failedLinesOfCode");
                    //result.LanguagesDetected = ((JArray)scanState["languageStateCollection"]).Select(x => x["languageName"].ToString()).ToList();

                    Console.WriteLine(JsonConvert.SerializeObject(result));
                }

                Console.WriteLine("== OSA Results ==");
                foreach (var osaScanUI in client.GetOSAScans(item.Key))
                {
                    var osaResults = client.GetOSAResults(osaScanUI);
                    Console.WriteLine(osaResults.ToString());
                }
                Console.WriteLine(" === == === == ");
            }
        }


        [TestMethod]
        public void GetPreset()
        {
            var presets = client.GetPresets();

            foreach (var item in client.GetProjects())
            {
                Trace.WriteLine($"{item.Key} " + client.GetSASTPreset(item.Key));
            }


        }

        [TestMethod]
        public void SetScanSettings()
        {
            foreach (var project in client.GetProjects())
            {
                client.SetProjectSettings(project.Key, 36,
                    1, 1);
            }
        }

        [TestMethod]
        public void GetProjectSettings()
        {
            client.GetProjectSettings(9);
        }

        [TestMethod]
        public void SetCustomFieldsTest()
        {
            var projectSettings = client.GetProjectSettings(20);

            client.SetCustomFields(projectSettings, new[] {
                new CustomField
                {
                    Id = 3,
                    Value = "Onboarded"
                }
            });
        }

        [TestMethod]
        public void TestCreationDate()
        {
            client.GetProjectCreationDate(9);

        }
        [TestMethod]
        public void TestCreationDateV9()
        {
            clientV9.GetProjectCreationDate(9);
        }

        [TestMethod]
        public void GetProjectDetails()
        {
            foreach (var item in client.GetAllProjectsDetails())
            {
                foreach (var customField in item.CustomFields)
                {
                    
                }
            }

        }
    }
}