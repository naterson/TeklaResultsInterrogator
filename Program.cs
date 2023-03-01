using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using TSD.API.Remoting;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.Document;
using TSD.Rpc.Analysis;
using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Sections;

namespace TeklaResultsInterrogator
{
    class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("Hello, World!");

            // Get the Active Model
            Console.WriteLine("\nSearching for Active Model...");
            TSD.API.Remoting.Structure.IModel? model = await GetActiveModelAsync();
            if (model == null)
            {
                Console.WriteLine("Routine has ended because model could not be found.");
                return;
            }

            // Get Analysis Results
            Console.WriteLine("\nSearching for Analysis Results...");
            AnalysisType analysisType = AnalysisType.FirstOrderLinear;
            LoadingType loadingType = LoadingType.LoadCombination;
            List<ILoadingCase>? loading = await GetRequestedLoadingAsync(model, analysisType, loadingType);
            if (loading == null)
            {
                Console.WriteLine("Routine has ended because loading could not be found.");
                return;
            }

            #region Get Members
            Console.WriteLine("\nSearching for Members...");
            MemberConstruction requestedMemberConstruction = MemberConstruction.TimberBeam;
            IEnumerable<IMember> allMembers = await model.GetMembersAsync(null);
            if (!allMembers.Any())
            {
                Console.WriteLine("Model has no members!");
                return;
            }
            List<IMember> members = allMembers.Where(c => c.Data.Value.Construction.Value.Equals(requestedMemberConstruction)).ToList();
            if (!members.Any())
            {
                Console.WriteLine("Model has no members matching requested construction type!");
                return;
            }
            Console.WriteLine($"Requested Member Construction: {requestedMemberConstruction}");
            Console.WriteLine($"{members.Count} {requestedMemberConstruction}s found.");
            #endregion

            #region Extracting Timber Beam Information
            Console.WriteLine("\nExtracting Timber Beam Information...");

            IApplication tsdInstance = await ApplicationFactory.GetFirstRunningApplicationAsync();
            string appTitle = await tsdInstance.GetApplicationTitleAsync();
            appTitle = appTitle.Split(" (")[0];
            string teklaDataPath = @"C:\Users\nnickerson\OneDrive - LeMessurier Consultants\Desktop\teklaData\";
            string beamDataPath = teklaDataPath + appTitle + " BeamData.csv";
            Console.WriteLine($"Saving to: {beamDataPath}");

            string headers = String.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}\n",
                "Beam #", "Breadth [in]", "Depth [in]", "Area [in^2]", "Section Modulus [in^3]",
                "Maximum Shear [k]", "Critical Shear Combination",
                "Maximum Moment [k-ft]", "Critical Moment Combination",
                "Bearing Length [in]", "Bending Stress [psi]", "Shear Stress [psi]",
                "Perpendicular Compression Stress [psi]");
            File.WriteAllText(beamDataPath, "");
            File.AppendAllText(beamDataPath, headers);

            foreach (IMember beam in members)
            {
                string name = beam.Name;
                IEnumerable<IMemberSpan> spans = await beam.GetSpanAsync();
                Console.WriteLine($"Beam {name} ({beam.SpanCount.Value} spans.)");

                foreach (IMemberSpan span in spans)
                {
                    IMemberSection? spanElementSection = span.ElementSection.Value as IMemberSection;
                    ITimberBeamSection? spanSection = spanElementSection.PhysicalSection.Value as ITimberBeamSection;
                    int spanIndex = span.Index;
                    string spanName = span.Name;
                    double spanLength = Math.Round(span.Length.Value / 25.4, 3);
                    double spanBreadth = Math.Round(spanSection.Breadth / 25.4, 3);
                    double spanDepth = Math.Round(spanSection.Depth / 25.4, 3);
                    double spanArea = spanBreadth * spanDepth;
                    double spanRot = Math.Round(span.RotationAngle.Value * 180 / Math.PI, 2);
                    double spanSectionModulus = Math.Round(spanBreadth * Math.Pow(spanDepth, 2) / 6, 3);
                    RotationOption spanRotOpt = span.RotationOption.Value;
                    Console.WriteLine($"\t{spanName} ({spanBreadth}\"x{spanDepth}\"): {spanLength}\" @ {spanRot}* {spanRotOpt}");

                    double momentMax = 0;
                    string criticalMomentCombination = "";
                    double shearMax = 0;
                    string criticalShearCombination = "";

                    foreach (ILoadingCase load in loading)
                    {
                        IMemberLoading memberLoading = await beam.GetLoadingAsync(load.Id, analysisType, LoadingResultType.Base);

                        LoadingValueOptions momentValueOption = new LoadingValueOptions(LoadingValueType.Moment, LoadingDirection.Major);
                        List<IPointOfInterest> maxMomentPOIs = (await memberLoading.GetPointsOfInterest(momentValueOption, PointOfInterestType.Maximum)).ToList();
                        maxMomentPOIs.AddRange(await memberLoading.GetPointsOfInterest(momentValueOption, PointOfInterestType.Minimum));
                        foreach (IPointOfInterest maxMomentPOI in maxMomentPOIs.Where(p => p.SpanIndex == span.Index))
                        {
                            IEnumerable<ILoadingValue> maxMomentLoadingValues = await memberLoading.GetValueAsync(momentValueOption, span.Index, maxMomentPOI.Position);
                            double maxMoment = maxMomentLoadingValues.OrderByDescending(lv => Math.Abs(lv.Value)).FirstOrDefault().Value;
                            maxMoment = Math.Round(Math.Abs(maxMoment) * 0.0000007375623, 4);
                            if (Math.Abs(maxMoment) > Math.Abs(momentMax))
                            {
                                momentMax = maxMoment;
                                criticalMomentCombination = load.Name;
                            }
                        }

                        LoadingValueOptions shearValueOption = new LoadingValueOptions(LoadingValueType.Force, LoadingDirection.Major);
                        List<IPointOfInterest> maxShearPOIs = (await memberLoading.GetPointsOfInterest(shearValueOption, PointOfInterestType.Maximum)).ToList();
                        maxShearPOIs.AddRange(await memberLoading.GetPointsOfInterest(shearValueOption, PointOfInterestType.Minimum));
                        foreach (IPointOfInterest maxShearPOI in maxShearPOIs.Where(p => p.SpanIndex == span.Index))
                        {
                            IEnumerable<ILoadingValue> maxShearLoadingValues = await memberLoading.GetValueAsync(shearValueOption, span.Index, maxShearPOI.Position);
                            double maxShear = maxShearLoadingValues.OrderByDescending(lv => Math.Abs(lv.Value)).FirstOrDefault().Value;
                            maxShear = Math.Round(Math.Abs(maxShear) * 0.0002248089, 4);
                            if (Math.Abs(maxShear) > Math.Abs(shearMax))
                            {
                                shearMax = maxShear;
                                criticalShearCombination = load.Name;
                            }
                        }
                    }

                    Console.WriteLine($"\t\tShear:  {shearMax} [k] from {criticalShearCombination}");
                    Console.WriteLine($"\t\tMoment: {momentMax} [k-ft] from {criticalMomentCombination}");

                    double bearingLength = 4;
                    double fb = momentMax / (spanSectionModulus * 12) * 1000;
                    double fv = (3 * shearMax) / (2 * spanBreadth * spanDepth) * 1000;
                    double fcp = shearMax / spanArea * 1000;

                    string beamData = String.Format("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}\n",
                        spanName, spanBreadth, spanDepth, spanArea, spanSectionModulus,
                        shearMax, criticalShearCombination, momentMax, criticalMomentCombination,
                        bearingLength, fb, fv, fcp);
                    File.AppendAllText(beamDataPath, beamData);

                }
            }

            #endregion

        }

        private static async Task<TSD.API.Remoting.Structure.IModel?> GetActiveModelAsync()
        {
            // Get Application
            IApplication? tsdInstance = await ApplicationFactory.GetFirstRunningApplicationAsync();
            if (tsdInstance == null)
            {
                Console.WriteLine("No running instances of TSD found!");
                return null;
            }
            string tsdVersion = await tsdInstance.GetVersionStringAsync();
            string appTitle = await tsdInstance.GetApplicationTitleAsync();
            appTitle = appTitle.Split(" (")[0];

            // Get Document
            IDocument? document = await tsdInstance.GetDocumentAsync();
            if (document == null)
            {
                Console.WriteLine("No active Document found!");
                return null;
            }
            string? docPath = document.Path;
            if (docPath == null || docPath == "")
            {
                Console.WriteLine("Active Document not yet saved!");
                return null;
            }

            // Get Model
            TSD.API.Remoting.Structure.IModel? model = await document.GetModelAsync();
            if (model == null)
            {
                Console.WriteLine("No Model found within Document!");
                return null;
            }

            // Write to Console
            Console.WriteLine($"Application found running TSD Ver. {tsdVersion}");
            Console.WriteLine($"Application title: {appTitle}");
            Console.WriteLine($"Document Path: {docPath}");
            return model;
        }

        private static async Task<List<ILoadingCase>?> GetRequestedLoadingAsync(TSD.API.Remoting.Structure.IModel model, AnalysisType requestedAnalysisType, LoadingType requestedLoadingType)
        {
            // Get Solver Model for requested analysis type
            IEnumerable<TSD.API.Remoting.Solver.IModel> solverModels = await model.GetSolverModelsAsync(new[] { requestedAnalysisType });
            if (!solverModels.Any())
            {
                Console.WriteLine("No solver models found!");
                return null;
            }
            TSD.API.Remoting.Solver.IModel? solverModel = solverModels.FirstOrDefault();
            if (solverModel == null)
            {
                Console.WriteLine("No solver model found for requested analysis type!");
                return null;
            }

            // Get Analysis Results
            IAnalysisResults? solverResults = await solverModel.GetResultsAsync();
            if (solverResults == null)
            {
                Console.WriteLine("No results found for requested analysis type!");
                return null;
            }
            IAnalysis3DResults? analysis3Dresults = await solverResults.GetAnalysis3DAsync();
            if (analysis3Dresults == null)
            {
                Console.WriteLine("No 3-D analysis results found for requested analysis type!");
                return null;
            }
            var solvedLoadingGuids = await analysis3Dresults.GetSolvedLoadingIdsAsync();
            if (!solvedLoadingGuids.Any())
            {
                Console.WriteLine("No solved loading GUIDs found!");
                return null;
            }

            // Get analysis results for requested loading type
            IEnumerable<ILoadingCase> loading;
            switch (requestedLoadingType)
            {
                case LoadingType.LoadCase:
                    loading = await model.GetLoadcasesAsync(null);
                    break;
                case LoadingType.LoadCombination:
                    loading = await model.GetCombinationsAsync(null);
                    break;
                default:
                    Console.WriteLine("Requested LoadingType invalid!");
                    return null;
            }
            if (loading == null || !loading.Any())
            {
                Console.WriteLine("No loading available for requested LoadingType!");
                return null;
            }
            var solvedLoading = loading.Where(l => solvedLoadingGuids.Contains(l.Id)).ToList();
            if (solvedLoading == null || !solvedLoading.Any())
            {
                Console.WriteLine("No analysis results available for requested analysis and loading types!");
                return null;
            }

            // Write To Console
            Console.WriteLine($"Requested Analysis Type: {requestedAnalysisType}");
            Console.WriteLine($"Requested Loading Type: {requestedLoadingType}");
            Console.WriteLine($"{solvedLoading.Count} Analysis Results Found.");
            return solvedLoading;

        }

        internal enum LoadingType
        {
            LoadCase,
            LoadCombination
                // want to add envelope here too?
        }
    }
}