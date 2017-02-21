﻿// <copyright file="ResolverVer1_1.cs" company="Google Inc.">
// Copyright (C) 2015 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>

namespace GooglePlayServices
{
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;
    using Google.JarResolver;
    using System;
    using System.Collections;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Xml;

    public class ResolverVer1_1 : DefaultResolver
    {
        // Caches data associated with an aar so that it doesn't need to be queried to determine
        // whether it should be expanded / exploded if it hasn't changed.
        private class AarExplodeData
        {
            // Identifier for an ABI independent AAR.
            public const string ABI_UNIVERSAL = "universal";
            // Time the file was modified the last time it was inspected.
            public System.DateTime modificationTime;
            // Whether the AAR file should be expanded / exploded.
            public bool explode = false;
            // Project's bundle ID when this was expanded.
            public string bundleId = "";
            // Path of the target AAR package.
            public string path = "";
            // Target ABI when this was expanded.
            public string targetAbi = ABI_UNIVERSAL;
            // Whether gradle is selected as the build system.
            public bool gradleBuildSystem = PlayServicesResolver.GradleBuildEnabled;
            // Whether gradle export is enabeld.
            public bool gradleExport = PlayServicesResolver.GradleProjectExportEnabled;
        }

        // Data that should be stored in the explode cache.
        private Dictionary<string, AarExplodeData> aarExplodeData =
            new Dictionary<string, AarExplodeData>();
        // Data currently stored in the explode cache.
        private Dictionary<string, AarExplodeData> aarExplodeDataSaved =
            new Dictionary<string, AarExplodeData>();
        // File used to to serialize aarExplodeData.  This is required as Unity will reload classes
        // in the editor when C# files are modified.
        private string aarExplodeDataFile = Path.Combine("Temp", "GoogleAarExplodeCache.xml");

        private const int MajorVersion = 1;
        private const int MinorVersion = 1;
        private const int PointVersion = 0;

        public ResolverVer1_1() {
            LoadAarExplodeCache();
        }

        /// <summary>
        /// Compare two dictionaries of AarExplodeData.
        /// </summary>
        private bool CompareExplodeData(Dictionary<string, AarExplodeData> explodeData1,
                                        Dictionary<string, AarExplodeData> explodeData2) {
            if (explodeData1 == explodeData2) return true;
            if (explodeData1 == null || explodeData2 == null) return false;
            if (explodeData1.Count != explodeData2.Count) return false;
            foreach (var item in explodeData1) {
                AarExplodeData data = null;
                if (!explodeData2.TryGetValue(item.Key, out data)) return false;
                if (!item.Value.Equals(data)) return false;
            }
            return true;
        }

        /// <summary>
        /// Load data cached in aarExplodeDataFile into aarExplodeData.
        /// </summary>
        private void LoadAarExplodeCache() {
            if (!File.Exists(aarExplodeDataFile)) return;

            XmlTextReader reader = new XmlTextReader(new StreamReader(aarExplodeDataFile));
            aarExplodeData.Clear();
            while (reader.Read()) {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "aars") {
                    while (reader.Read()) {
                        if (reader.NodeType == XmlNodeType.Element &&
                            reader.Name == "explodeData") {
                            string aar = "";
                            AarExplodeData aarData = new AarExplodeData();
                            do {
                                if (!reader.Read()) break;
                                if (reader.NodeType == XmlNodeType.Element) {
                                    string elementName = reader.Name;
                                    if (reader.Read() && reader.NodeType == XmlNodeType.Text) {
                                        if (elementName == "aar") {
                                            aar = reader.ReadContentAsString();
                                        } else if (elementName == "modificationTime") {
                                            aarData.modificationTime =
                                                reader.ReadContentAsDateTime();
                                        } else if (elementName == "explode") {
                                            aarData.explode = reader.ReadContentAsBoolean();
                                        } else if (elementName == "bundleId") {
                                            aarData.bundleId = reader.ReadContentAsString();
                                        } else if (elementName == "path") {
                                            aarData.path = reader.ReadContentAsString();
                                        } else if (elementName == "targetAbi") {
                                            aarData.targetAbi = reader.ReadContentAsString();
                                        } else if (elementName == "gradleBuildSystem") {
                                            aarData.gradleBuildSystem =
                                                reader.ReadContentAsBoolean();
                                        } else if (elementName == "gradleExport") {
                                            aarData.gradleExport = reader.ReadContentAsBoolean();
                                        }
                                    }
                                }
                            } while (!(reader.Name == "explodeData" &&
                                       reader.NodeType == XmlNodeType.EndElement));
                            if (aar != "" && aarData.path != "") aarExplodeData[aar] = aarData;
                        }
                    }
                }
            }
            aarExplodeDataSaved = new Dictionary<string, AarExplodeData>(aarExplodeData);
        }

        /// <summary>
        /// Save data from aarExplodeData into aarExplodeDataFile.
        /// </summary>
        private void SaveAarExplodeCache()
        {
            if (File.Exists(aarExplodeDataFile))
            {
                // If the explode data hasn't been modified, don't save.
                if (CompareExplodeData(aarExplodeData, aarExplodeDataSaved)) return;
                File.Delete(aarExplodeDataFile);
            }
            XmlTextWriter writer = new XmlTextWriter(new StreamWriter(aarExplodeDataFile)) {
                Formatting = Formatting.Indented,
            };
            writer.WriteStartElement("aars");
            foreach (KeyValuePair<string, AarExplodeData> kv in aarExplodeData)
            {
                writer.WriteStartElement("explodeData");
                writer.WriteStartElement("aar");
                writer.WriteValue(kv.Key);
                writer.WriteEndElement();
                writer.WriteStartElement("modificationTime");
                writer.WriteValue(kv.Value.modificationTime);
                writer.WriteEndElement();
                writer.WriteStartElement("explode");
                writer.WriteValue(kv.Value.explode);
                writer.WriteEndElement();
                writer.WriteStartElement("bundleId");
                writer.WriteValue(PlayerSettings.bundleIdentifier);
                writer.WriteEndElement();
                writer.WriteStartElement("path");
                writer.WriteValue(kv.Value.path);
                writer.WriteEndElement();
                writer.WriteStartElement("targetAbi");
                writer.WriteValue(kv.Value.targetAbi);
                writer.WriteEndElement();
                writer.WriteStartElement("gradleBuildEnabled");
                writer.WriteValue(kv.Value.gradleBuildSystem);
                writer.WriteEndElement();
                writer.WriteStartElement("gradleExport");
                writer.WriteValue(kv.Value.gradleExport);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.Flush();
            writer.Close();
            aarExplodeDataSaved = new Dictionary<string, AarExplodeData>(aarExplodeData);
        }

        /// <summary>
        /// Find a tool in the Android SDK.
        /// </summary>
        /// <param name="svcSupport">PlayServicesSupport instance used to retrieve the SDK
        /// path. </param>
        /// <param name="toolName">Name of the tool to search for.</param>
        /// <returns>String with the path to the tool if found, null otherwise.</returns>
        internal static string FindAndroidSdkTool(PlayServicesSupport svcSupport, string toolName)
        {
            string toolPath = null;
            string sdkPath = svcSupport.SDK;
            if (sdkPath == null || sdkPath == "")
            {
                Debug.LogWarning(PlayServicesSupport.AndroidSdkConfigurationError +
                                 "  Will fallback to searching for " + toolName +
                                 " in the system path.");
            }
            else
            {
                string[] extensions;
                if (UnityEngine.RuntimePlatform.WindowsEditor ==
                    UnityEngine.Application.platform) {
                    extensions = new string[] { CommandLine.GetExecutableExtension(),
                                                ".bat", ".cmd" };
                } else {
                    extensions = new string[] { CommandLine.GetExecutableExtension() };
                }
                foreach (var extension in extensions) {
                  toolPath = Path.Combine(sdkPath, Path.Combine("tools", toolName + extension));
                  if (File.Exists(toolPath)) {
                      break;
                  }
                }
            }
            if (toolPath == null || !File.Exists(toolPath))
            {
                toolPath = CommandLine.FindExecutable(toolName);
            }
            return toolPath;
        }

        /// <summary>
        /// Generate an array from a string collection.
        /// </summary>
        /// <returns>An array of strings.</return>
        private static string[] CollectionToArray(ICollection enumerator)
        {
            return (string[])(new ArrayList(enumerator)).ToArray(typeof(string));
        }

        /// <summary>
        /// Delegate called when GetAvailablePackages() completes.
        /// </summary>
        internal delegate void GetAvailablePackagesComplete(Dictionary<string, bool> packages);

        // Answers Android SDK manager license questions.
        private class LicenseResponder : CommandLine.LineReader
        {
            private const string Question = "Do you accept the license";

            private string response;

            // Initialize the class to respond "yes" or "no" to license questions.
            public LicenseResponder(bool accept)
            {
                LineHandler += CheckAndRespond;
                response = accept ? "yes" : "no";
            }

            // Respond license questions with the "response".
            public void CheckAndRespond(System.Diagnostics.Process process, StreamWriter stdin,
                                        CommandLine.StreamData data)
            {
                if (process.HasExited) return;
                if ((data.data != null && data.text.Contains(Question)) ||
                    CommandLine.LineReader.Aggregate(GetBufferedData(0)).text.Contains(Question))
                {
                    Flush();
                    // Ignore I/O exceptions as this could race with the process exiting.
                    try
                    {
                        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(
                                     response + System.Environment.NewLine))
                        {
                            stdin.BaseStream.WriteByte(b);
                        }
                        stdin.BaseStream.Flush();
                    }
                    catch (System.IO.IOException)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Get the set of available SDK packages and whether they're installed.
        /// </summary>
        /// <param name="androidTool">Path to the Android SDK manager tool.</param>
        /// <param name="svcSupport">PlayServicesSupport instance used to retrieve the SDK
        /// path.</param>
        /// <param name="packages">Delegate called with a dictionary of package names and whether
        /// they're installed or null if the Android SDK isn't configured correctly.</param>
        internal static void GetAvailablePackages(
            string androidTool, PlayServicesSupport svcSupport,
            GetAvailablePackagesComplete complete)
        {
            CommandLineDialog window = CommandLineDialog.CreateCommandLineDialog(
                "Get Installed Android SDK packages.");
            window.modal = false;
            window.summaryText = "Getting list of installed Android packages.";
            window.progressTitle = window.summaryText;
            window.autoScrollToBottom = true;
            window.RunAsync(
                androidTool, "list sdk -u -e -a",
                (result) => {
                    window.Close();
                    if (result.exitCode != 0)
                    {
                        Debug.LogError("Unable to determine which Android packages are " +
                                       "installed.  Failed to run " + androidTool + ".  " +
                                       result.stderr + " (" + result.exitCode.ToString() + ")");
                        complete(null);
                        return;
                    }
                    Dictionary<string, bool> packages = new Dictionary<string, bool>();
                    string[] lines = Regex.Split(result.stdout, "\r\n|\r|\n");
                    string packageIdentifier = null;
                    foreach (string line in lines)
                    {
                        // Find the start of a package description.
                        Match match = Regex.Match(line, "^id:\\W+\\d+\\W+or\\W+\"([^\"]+)\"");
                        if (match.Success)
                        {
                            packageIdentifier = match.Groups[1].Value;
                            packages[packageIdentifier] = false;
                            continue;
                        }
                        if (packageIdentifier == null)
                        {
                            continue;
                        }
                        // Parse the install path and record whether the package is installed.
                        match = Regex.Match(line, "^\\W+Install[^:]+:\\W+([^ ]+)");
                        if (match.Success)
                        {
                            packages[packageIdentifier] = File.Exists(
                                Path.Combine(Path.Combine(svcSupport.SDK, match.Groups[1].Value),
                                    "source.properties"));
                            packageIdentifier = null;
                        }
                    }
                    complete(packages);
                },
                maxProgressLines: 50);
            window.Show();
        }

        #region IResolver implementation

        /// <summary>
        /// Version of the resolver. - 1.1.0
        /// </summary>
        /// <remarks>The resolver with the greatest version is used when resolving.
        /// The value of the verison is calcuated using MakeVersion in DefaultResolver</remarks>
        /// <seealso cref="DefaultResolver.MakeVersionNumber"></seealso>
        public override int Version()
        {
            return MakeVersionNumber(MajorVersion, MinorVersion, PointVersion);
        }

        /// <summary>
        /// Perform the resolution and the exploding/cleanup as needed.
        /// </summary>
        public override void DoResolution(
            PlayServicesSupport svcSupport, string destinationDirectory,
            PlayServicesSupport.OverwriteConfirmation handleOverwriteConfirmation,
            System.Action resolutionComplete)
        {
            System.Action resolve = () => {
                DoResolutionNoAndroidPackageChecks(svcSupport, destinationDirectory,
                                                   handleOverwriteConfirmation);
                resolutionComplete();
            };

            var dependencies = svcSupport.DependenciesPresent(destinationDirectory);
            if (dependencies == null) return;

            // Set of packages that need to be installed.
            Dictionary<string, bool> installPackages = new Dictionary<string, bool>();
            // Retrieve the set of required packages and whether they're installed.
            Dictionary<string, Dictionary<string, bool>> requiredPackages =
                new Dictionary<string, Dictionary<string, bool>>();
            foreach (Dependency dependency in
                     svcSupport.LoadDependencies(true, keepMissing: true).Values)
            {
                if (dependency.PackageIds != null)
                {
                    foreach (string packageId in dependency.PackageIds)
                    {
                        Dictionary<string, bool> dependencySet;
                        if (!requiredPackages.TryGetValue(packageId, out dependencySet))
                        {
                            dependencySet = new Dictionary<string, bool>();
                        }
                        dependencySet[dependency.Key] = false;
                        requiredPackages[packageId] = dependencySet;
                        // If the dependency is missing, add it to the set that needs to be
                        // installed.
                        if (System.String.IsNullOrEmpty(dependency.BestVersionPath))
                        {
                            installPackages[packageId] = false;
                        }
                    }
                }
            }

            // If no packages need to be installed or Android SDK package installation is disabled.
            if (installPackages.Count == 0 || !AndroidPackageInstallationEnabled())
            {
                // Report missing packages as warnings and try to resolve anyway.
                foreach (string pkg in requiredPackages.Keys)
                {
                    string depString = System.String.Join(
                        ", ", CollectionToArray(requiredPackages[pkg].Keys));
                    if (installPackages.ContainsKey(pkg) && depString.Length > 0)
                    {
                        Debug.LogWarning(pkg + " not installed or out of date!  This is " +
                                         "required by the following dependencies " + depString);
                    }
                }
                // Attempt resolution.
                resolve();
                return;
            }

            // Find the Android SDK manager.
            string sdkPath = svcSupport.SDK;
            string androidTool = FindAndroidSdkTool(svcSupport, "android");
            if (androidTool == null || sdkPath == null || sdkPath == "")
            {
                Debug.LogError("Unable to find the Android SDK manager tool.  " +
                               "Required Android packages (" +
                               System.String.Join(", ", CollectionToArray(installPackages.Keys)) +
                               ") can not be installed.  " +
                               PlayServicesSupport.AndroidSdkConfigurationError);
                return;
            }

            // Get the set of available and installed packages.
            GetAvailablePackages(
                androidTool, svcSupport,
                (Dictionary<string, bool> packageInfo) => {
                    if (packageInfo == null)
                    {
                        return;
                    }

                    // Filter the set of packages to install by what is available.
                    foreach (string pkg in requiredPackages.Keys)
                    {
                        bool installed = false;
                        string depString = System.String.Join(
                            ", ", CollectionToArray(requiredPackages[pkg].Keys));
                        if (packageInfo.TryGetValue(pkg, out installed))
                        {
                            if (!installed)
                            {
                                installPackages[pkg] = false;
                                Debug.LogWarning(pkg + " not installed or out of date!  " +
                                                 "This is required by the following " +
                                                 "dependencies " + depString);
                            }
                        }
                        else
                        {
                            Debug.LogWarning(pkg + " referenced by " + depString +
                                             " not available in the Android SDK.  This " +
                                             "package will not be installed.");
                            installPackages.Remove(pkg);
                        }
                    }

                    if (installPackages.Count == 0)
                    {
                        resolve();
                        return;
                    }

                    // Start installation.
                    string installPackagesString = System.String.Join(
                        ",", CollectionToArray(installPackages.Keys));
                    string packagesCommand = "update sdk -a -u -t " + installPackagesString;
                    CommandLineDialog window = CommandLineDialog.CreateCommandLineDialog(
                        "Install Android SDK packages");
                    window.summaryText = "Retrieving licenses...";
                    window.modal = false;
                    window.progressTitle = window.summaryText;
                    window.RunAsync(
                        androidTool, packagesCommand,
                        (CommandLine.Result getLicensesResult) => {
                            // Get the start of the license text.
                            int licenseTextStart = getLicensesResult.stdout.IndexOf("--------");
                            if (getLicensesResult.exitCode != 0 || licenseTextStart < 0)
                            {
                                window.Close();
                                Debug.LogError("Unable to retrieve licenses for packages " +
                                               installPackagesString);
                                return;
                            }

                            // Remove the download output from the string.
                            string licenseText = getLicensesResult.stdout.Substring(
                                licenseTextStart);
                            window.summaryText = ("License agreement(s) required to install " +
                                                  "Android SDK packages");
                            window.bodyText = licenseText;
                            window.yesText = "agree";
                            window.noText = "decline";
                            window.result = false;
                            window.Repaint();
                            window.buttonClicked = (TextAreaDialog dialog) => {
                                if (!dialog.result)
                                {
                                    window.Close();
                                    return;
                                }

                                window.summaryText = "Installing Android SDK packages...";
                                window.bodyText = "";
                                window.yesText = "";
                                window.noText = "";
                                window.buttonClicked = null;
                                window.progressTitle = window.summaryText;
                                window.autoScrollToBottom = true;
                                window.Repaint();
                                // Kick off installation.
                                ((CommandLineDialog)window).RunAsync(
                                    androidTool, packagesCommand,
                                    (CommandLine.Result updateResult) => {
                                        window.Close();
                                        if (updateResult.exitCode == 0)
                                        {
                                            resolve();
                                        }
                                        else
                                        {
                                            Debug.LogError("Android SDK update failed.  " +
                                                           updateResult.stderr + "(" +
                                                           updateResult.exitCode.ToString() + ")");
                                        }
                                    },
                                    ioHandler: (new LicenseResponder(true)).AggregateLine,
                                    maxProgressLines: 500);
                            };
                        },
                        ioHandler: (new LicenseResponder(false)).AggregateLine,
                        maxProgressLines: 250);
                });
        }


        /// <summary>
        /// Called during Update to allow the resolver to check any build settings of managed
        /// packages to see whether resolution should be triggered again.
        /// </summary>
        /// <returns>Array of packages that should be re-resolved if resolution should occur,
        /// null otherwise.</returns>
        public override string[] OnBuildSettings() {
            // Determine which packages need to be updated.
            List<string> packagesToUpdate = new List<string>();
            List<string> aarsToResolve = new List<string>();
            var aarExplodeDataCopy = new Dictionary<string, AarExplodeData>(aarExplodeData);
            foreach (var kv in aarExplodeDataCopy) {
                var aar = kv.Key;
                var aarData = kv.Value;
                // If the cached file has been removed, ditch it from the cache.
                if (!(File.Exists(aarData.path) || Directory.Exists(aarData.path))) {
                    aarsToResolve.Add(aar);
                } else if (AarExplodeDataIsDirty(aarData)) {
                    packagesToUpdate.Add(aarData.path);
                    aarsToResolve.Add(aar);
                }
            }
            // Remove AARs that will be resolved from the dictionary so the next call to
            // OnBundleId triggers another resolution process.
            foreach (string aar in aarsToResolve) aarExplodeData.Remove(aar);
            SaveAarExplodeCache();
            return packagesToUpdate.Count > 0 ? packagesToUpdate.ToArray() : null;
        }
        #endregion

        /// <summary>
        /// Convert an AAR filename to package name.
        /// </summary>
        /// <param name="aarPath">Path of the AAR to convert.</param>
        /// <returns>AAR package name.</returns>
        private string AarPathToPackageName(string aarPath) {
            var aarFilename = Path.GetFileName(aarPath);
            foreach (var extension in Dependency.Packaging) {
                if (aarPath.EndsWith(extension)) {
                    return aarFilename.Substring(0, aarFilename.Length - extension.Length);
                }
            }
            return aarFilename;
        }

        /// <summary>
        /// Search the cache for an entry associated with the specified AAR path.
        /// </summary>
        /// <param name="aarPath">Path of the AAR to query.</param>
        /// <returns>AarExplodeData if the entry is found in the cache, null otherwise.</returns>
        private AarExplodeData FindAarExplodeDataEntry(string aarPath) {
            var aarFilename = AarPathToPackageName(aarPath);
            AarExplodeData aarData = null;
            // The argument to this method could be the exploded folder rather than the source
            // package (e.g some-package rather than some-package.aar).  Therefore search the
            // cache for the original / unexploded package if the specified file isn't found.
            var packageExtensions = new List<string>();
            packageExtensions.Add("");  // Search for aarFilename first.
            packageExtensions.AddRange(Dependency.Packaging);
            foreach (var extension in packageExtensions) {
                AarExplodeData data;
                if (aarExplodeData.TryGetValue(aarFilename + extension, out data)) {
                    aarData = data;
                    break;
                }
            }
            return aarData;
        }

        /// <summary>
        /// Determine whether a package is dirty in the AAR cache.
        /// </summary>
        /// <param name="aarData">Path of the AAR to query.</param>
        /// <returns>true if the cache entry is dirty, false otherwise.</returns>
        private bool AarExplodeDataIsDirty(AarExplodeData aarData) {
            return aarData.bundleId != PlayerSettings.bundleIdentifier ||
                (aarData.targetAbi != AarExplodeData.ABI_UNIVERSAL &&
                 aarData.targetAbi != PlayServicesResolver.AndroidTargetDeviceAbi) ||
                aarData.gradleBuildSystem != PlayServicesResolver.GradleBuildEnabled ||
                aarData.gradleExport != PlayServicesResolver.GradleProjectExportEnabled ||
                (aarData.explode ? !Directory.Exists(aarData.path) : !File.Exists(aarData.path));
        }

        /// <summary>
        /// Perform resolution with no Android package dependency checks.
        /// </summary>
        private void DoResolutionNoAndroidPackageChecks(
            PlayServicesSupport svcSupport, string destinationDirectory,
            PlayServicesSupport.OverwriteConfirmation handleOverwriteConfirmation)
        {
            try
            {
                // Get the collection of dependencies that need to be copied.
                Dictionary<string, Dependency> deps =
                    svcSupport.ResolveDependencies(true, destDirectory: destinationDirectory,
                                                   explodeAar: (aarPath) => {
                                                       return ShouldExplode(aarPath);
                                                   });
                // Copy the list
                svcSupport.CopyDependencies(deps,
                                            destinationDirectory,
                                            handleOverwriteConfirmation);

            }
            catch (Google.JarResolver.ResolutionException e)
            {
                Debug.LogError(e.ToString());
                return;
            }

            // we want to look at all the .aars to decide to explode or not.
            // Some aars have variables in their AndroidManifest.xml file,
            // e.g. ${applicationId}.  Unity does not understand how to process
            // these, so we handle it here.
            ProcessAars(destinationDirectory);
        }

        /// <summary>
        /// Get the target path for an exploded AAR.
        /// </summary>
        /// <param name="aarPath">AAR file to explode.</param>
        /// <returns>Exploded AAR path.</returns>
        private string DetermineExplodedAarPath(string aarPath) {
            return Path.Combine(GooglePlayServices.SettingsDialog.PackageDir,
                                AarPathToPackageName(aarPath));
        }

        /// <summary>
        /// Processes the aars.
        /// </summary>
        /// <remarks>Each aar copied is inspected and determined if it should be
        /// exploded into a directory or not. Unneeded exploded directories are
        /// removed.
        /// <para>
        /// Exploding is needed if the version of Unity is old, or if the artifact
        /// has been explicitly flagged for exploding.  This allows the subsequent
        /// processing of variables in the AndroidManifest.xml file which is not
        /// supported by the current versions of the manifest merging process that
        /// Unity uses.
        /// </para>
        /// <param name="dir">The directory to process.</param>
        private void ProcessAars(string dir) {
            var aars = new HashSet<string>();
            // Build set of AAR files and directories.
            foreach (var aarFilename in Directory.GetFiles(dir, "*.aar")) {
                aars.Add(Path.Combine(dir, Path.GetFileName(aarFilename)));
            }
            foreach (var aarData in aarExplodeData.Values) aars.Add(aarData.path);
            foreach (string aarPath in aars) {
                bool explode = ShouldExplode(aarPath);
                var aarData = FindAarExplodeDataEntry(aarPath);
                if (AarExplodeDataIsDirty(aarData)) {
                    if (explode && File.Exists(aarPath)) {
                        ReplaceVariables(ProcessAar(Path.GetFullPath(dir), aarPath));
                        aarData.targetAbi =
                            AarDirectoryDetermineAbi(DetermineExplodedAarPath(aarPath));
                    } else {
                        // Clean up previously expanded / exploded versions of the AAR.
                        PlayServicesSupport.DeleteExistingFileOrDirectory(
                            DetermineExplodedAarPath(aarPath), includeMetaFiles: true);
                    }
                    aarData.gradleBuildSystem = PlayServicesResolver.GradleBuildEnabled;
                    aarData.gradleExport = PlayServicesResolver.GradleProjectExportEnabled;
                }
            }
            SaveAarExplodeCache();
        }

        /// <summary>
        /// Determine whether a Unity library project (extract AAR) contains native libraries.
        /// </summary>
        /// <return>ABI associated with the directory.</return>
        private string AarDirectoryDetermineAbi(string aarDirectory) {
            var foundAbis = new HashSet<string>();
            foreach (var libDirectory in NATIVE_LIBRARY_DIRECTORIES) {
                foreach (var kv in UNITY_ABI_TO_NATIVE_LIBRARY_ABI_DIRECTORY) {
                    var path = Path.Combine(libDirectory, kv.Value);
                    if (Directory.Exists(Path.Combine(aarDirectory, path))) {
                        foundAbis.Add(kv.Key);
                    }
                }
            }
            var numberOfAbis = foundAbis.Count;
            if (numberOfAbis > 0) {
                if (numberOfAbis == 1) foreach (var abi in foundAbis) return abi;
                return PlayServicesResolver.DEFAULT_ANDROID_TARGET_DEVICE_ABI;
            }
            return AarExplodeData.ABI_UNIVERSAL;
        }

        /// <summary>
        /// Determines whether an aar file should be exploded (extracted).
        ///
        /// This is required for some aars so that the Unity Jar Resolver can perform variable
        /// expansion on manifests in the package before they're merged by aapt.
        /// </summary>
        /// <returns><c>true</c>, if the aar should be exploded, <c>false</c> otherwise.</returns>
        /// <param name="aarPath">Path of the AAR file to query whether it should be exploded or
        /// the path to the exploded AAR directory to determine whether the AAR should still be
        /// exploded.</param>
        internal virtual bool ShouldExplode(string aarPath) {
            bool newAarData = false;
            AarExplodeData aarData = FindAarExplodeDataEntry(aarPath);
            if (aarData == null) {
                newAarData = true;
                aarData = new AarExplodeData();
                aarData.path = aarPath;
            }
            string explodeDirectory = DetermineExplodedAarPath(aarPath);
            bool explosionEnabled = true;
            // Unfortunately, as of Unity 5.5.0f3, Unity does not set the applicationId variable
            // in the build.gradle it generates.  This results in non-functional libraries that
            // require the ${applicationId} variable to be expanded in their AndroidManifest.xml.
            // To work around this when Gradle builds are enabled, explosion is enabled for all
            // AARs that require variable expansion unless this behavior is explicitly disabled
            // in the settings dialog.
            if (PlayServicesResolver.GradleProjectExportEnabled && !SettingsDialog.ExplodeAars) {
                explosionEnabled = false;
            }
            string targetAbi = AarExplodeData.ABI_UNIVERSAL;
            bool explode = false;
            if (explosionEnabled) {
                explode = !SupportsAarFiles;
                bool useCachedExplodeData = false;
                bool aarFile = File.Exists(aarPath);
                if (!explode) {
                    System.DateTime modificationTime =
                        aarFile ? File.GetLastWriteTime(aarPath) : System.DateTime.MinValue;
                    if (modificationTime.CompareTo(aarData.modificationTime) <= 0 &&
                        !AarExplodeDataIsDirty(aarData)) {
                        explode = aarData.explode;
                        useCachedExplodeData = true;
                    }
                }
                if (!explode) {
                    // If the path is a directory then the caller is referencing an AAR that has
                    // already been exploded in which case we keep explosion enabled.
                    string aarDirectory = Directory.Exists(explodeDirectory) ? explodeDirectory :
                        Directory.Exists(aarData.path) ? aarData.path : null;
                    if (!String.IsNullOrEmpty(aarDirectory)) {
                        // If the directory contains native libraries update the target ABI.
                        if (!useCachedExplodeData) {
                            newAarData = true;
                            targetAbi = AarDirectoryDetermineAbi(aarDirectory);
                        }
                        explode = true;
                    }
                }
                if (!useCachedExplodeData && !explode) {
                    // If the file doesn't exist we can't interrogate it so we can assume it
                    // doesn't need to be exploded.
                    if (!aarFile) return false;

                    string temporaryDirectory = CreateTemporaryDirectory();
                    if (temporaryDirectory == null) return false;
                    string manifestFilename = "AndroidManifest.xml";
                    try {
                        if (ExtractAar(aarPath, new string[] {manifestFilename, "jni"},
                                       temporaryDirectory)) {
                            string manifestPath = Path.Combine(temporaryDirectory,
                                                               manifestFilename);
                            if (File.Exists(manifestPath)) {
                                string manifest = File.ReadAllText(manifestPath);
                                explode = manifest.IndexOf("${applicationId}") >= 0;
                            }
                            // If the AAR contains more than one ABI and Unity's build is
                            // targeting a single ABI, explode it so that unused ABIs can be
                            // removed.
                            newAarData = true;
                            targetAbi = AarDirectoryDetermineAbi(temporaryDirectory);
                            // NOTE: Unfortunately as of Unity 5.5 the internal Gradle build
                            // also blindly includes all ABIs from AARs included in the project
                            // so we need to explode the AARs and remove unused ABIs.
                            explode |= targetAbi != AarExplodeData.ABI_UNIVERSAL &&
                                targetAbi != PlayServicesResolver.AndroidTargetDeviceAbi;
                            aarData.modificationTime = File.GetLastWriteTime(aarPath);
                        }
                    }
                    catch (System.Exception e) {
                        Debug.Log("Unable to examine AAR file " + aarPath + ", err: " + e);
                        throw e;
                    }
                    finally {
                        PlayServicesSupport.DeleteExistingFileOrDirectory(temporaryDirectory);
                    }
                }
            }
            // If this is a new cache entry populate the target ABI and bundle ID fields.
            if (newAarData) {
                aarData.targetAbi = targetAbi;
                aarData.bundleId = PlayerSettings.bundleIdentifier;
            }
            aarData.path = explode ? explodeDirectory : aarPath;
            aarData.explode = explode;
            aarExplodeData[AarPathToPackageName(aarPath)] = aarData;
            SaveAarExplodeCache();
            return explode;
        }

        /// <summary>
        /// Replaces the variables in the AndroidManifest file.
        /// </summary>
        /// <param name="exploded">Exploded.</param>
        void ReplaceVariables(string exploded)
        {
            string manifest = Path.Combine(exploded, "AndroidManifest.xml");
            if (File.Exists(manifest))
            {
                StreamReader sr = new StreamReader(manifest);
                string body = sr.ReadToEnd();
                sr.Close();

                body = body.Replace("${applicationId}", PlayerSettings.bundleIdentifier);

                using (var wr = new StreamWriter(manifest, false))
                {
                    wr.Write(body);
                }
            }
        }
    }
}
