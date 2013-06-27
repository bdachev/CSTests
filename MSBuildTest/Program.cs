using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using BE = Microsoft.Build.BuildEngine;

namespace MSBuildTest
{
    class Program
    {
        static void GetMetadataOldWay(string projName, string[] targets)
        {
            var logger = new BE.ConsoleLogger();
            var engine = new BE.Engine();
            engine.RegisterLogger(logger);
            var prj = new BE.Project(engine);
            prj.Load(projName);
            var outputs = new Dictionary<object, object>();
            prj.Build(targets, outputs);
            foreach (string target in targets)
            {
                foreach (ITaskItem item in outputs[target] as IEnumerable)
                {
                    Console.WriteLine(item.ItemSpec);
                    foreach (string metaname in item.MetadataNames)
                    {
                        Console.WriteLine("\t{0}:{1}", metaname, item.GetMetadata(metaname));
                    }
                    Console.ReadKey(false);
                }
            }
        }

        private static void GetMetadataNewWay(string projName, string[] targets)
        {
            var logger = new ConsoleLogger();
            var projCol = new ProjectCollection();
            var proj = projCol.LoadProject(projName);
            var projInst = proj.CreateProjectInstance();
            var buildManager = new BuildManager();
            var buildParams = new BuildParameters();
            buildParams.Loggers = new ILogger[] { logger };
            buildManager.BeginBuild(buildParams);
            var buildRequestData = new BuildRequestData(projInst, targets, null, BuildRequestDataFlags.ReplaceExistingProjectInstance);
            var submission = buildManager.PendBuildRequest(buildRequestData);
            var buildResult = submission.Execute();
            buildManager.EndBuild();
            var tryGetValue = buildResult.ResultsByTarget.GetType().GetMethod("TryGetValue");
            foreach (var target in targets)
            {
                Console.WriteLine("Target: {0}", target);
                var prms = new object[2] { target, null };
                if ((bool)tryGetValue.Invoke(buildResult.ResultsByTarget, prms))
                {
                    var targetResult = prms[1];
                    if (targetResult != null)
                    {
                        var items = targetResult.GetType().GetProperty("Items").GetValue(targetResult, null) as IEnumerable;
                        foreach (var item in items)
                        {

                            var metaNames = item.GetType().GetProperty("MetadataNames").GetValue(item, null) as IEnumerable;
                            foreach (object metaname in metaNames)
                            {
                                Console.WriteLine("\t{0}:{1}", metaname, item.GetType().GetMethod("GetMetadata").Invoke(item, new object[] { metaname }));
                            }
                        }
                    }
                }
                Console.ReadKey(false);
            }
        }

        //private static void GetMetadataNewWay(string projName, string[] targetNames)
        //{
        //    var logger = new ConsoleLogger();
        //    var projCol = new ProjectCollection();
        //    var proj = projCol.LoadProject(projName);
        //    var projInst = proj.CreateProjectInstance();
        //    var buildManager = new BuildManager();
        //    var buildParams = new BuildParameters();
        //    buildParams.Loggers = new ILogger[] { logger };
        //    buildManager.BeginBuild(buildParams);
        //    var buildRequestData = new BuildRequestData(projInst, targetNames, null, BuildRequestDataFlags.ReplaceExistingProjectInstance);
        //    var submission = buildManager.PendBuildRequest(buildRequestData);
        //    var buildResult = submission.Execute();
        //    buildManager.EndBuild();

        //    IDictionary<string, TargetResult> resultsByTarget = buildResult.ResultsByTarget;
        //    if (resultsByTarget != null)
        //    {
        //        // enumerate target names and for each try to get items from results of the build
        //        foreach (string itemName in targetNames)
        //        {
        //            TargetResult result;
        //            bool hasKey = resultsByTarget.TryGetValue(itemName, out result);
        //            if (hasKey)
        //            {
        //                if (result != null)
        //                {
        //                    ITaskItem[] items = result.Items;
        //                    targetOutputs.Add(itemName, items);
        //                }
        //            }

        //            // fill in empty lists for each target so that heat will look at the item group later
        //            if (!targetOutputs.Contains(itemName))
        //            {
        //                targetOutputs.Add(itemName, new List<object>());
        //            }
        //        }
        //    }
        //}

        [STAThread]
        static void Main(string[] args)
        {
            string projName = @"I:\SOURCE\Trio\MP v3\migrate.net4\Source\VS2008-CSHARP\Libraries\SharedLibrary\SharedLibrary.csproj";
            //string projName = @"I:\SOURCE\Trio\MP v3\migrate.net4\Source\VS2008-CSHARP\Libraries\HMIComponents\HMIComponents.csproj";
            string[] targets = {    "BuiltProjectOutputGroup",
                                    "DebugSymbolsProjectOutputGroup", 
                                    "DocumentationProjectOutputGroup",
                                    "SatelliteDllsProjectOutputGroup",
                                    "SourceFilesProjectOutputGroup",  
                                    "ContentFilesProjectOutputGroup" };

            //GetMetadataOldWay(projName, targets);
            GetMetadataNewWay(projName, targets);
            Console.ReadKey();
        }
    }
}
