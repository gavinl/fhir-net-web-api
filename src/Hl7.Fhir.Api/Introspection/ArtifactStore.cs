﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/spark/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;
using Hl7.Fhir.Rest;
using System.IO;
using Ionic.Zip;
using Hl7.Fhir.Serialization;
using System.Xml.XPath;

namespace Hl7.Fhir.Api.Profiles
{
    /// <summary>
    /// Reads FHIR artifacts (Profiles, ValueSets, ...) from (zipped) Bundles and individual files
    /// </summary>
    public class ArtifactStore
    {
        public const string CORE_SPEC_URI_PREFIX = "http://hl7.org/fhir/";
        public const string CORE_SPEC_PROFILE_URI_PREFIX = "http://hl7.org/fhir/profile/";
        public const string CORE_SPEC_CONFORMANCE_URI_PREFIX = "http://hl7.org/fhir/conformance/";
        public const string CORE_SPEC_VS_URI_PREFIX = "http://hl7.org/fhir/vs/";
        public const string CORE_SPEC_V2VS_URI_PREFIX = "http://hl7.org/fhir/v2/";
        public const string CORE_SPEC_V3VS_URI_PREFIX = "http://hl7.org/fhir/v3/";
        public const string CORE_SPEC_NAMESPACE_URI_PREFIX = "http://hl7.org/fhir/ns/";      //TODO: check prefix
        public const string CORE_SPEC_CONCEPTMAP_URI_PREFIX = "http://hl7.org/fhir/conceptmap/";     //TODO: check prefix

        private string _storeDirectory;
        private bool isPrepared = false;

        public ArtifactStore(string storeDirectory)
        {
            _storeDirectory = storeDirectory;
        }

        public ArtifactStore()
        {
            _storeDirectory = Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Unpacks zip-files containing the artifact files, and enumerates all (zipped/nonzipped) files.
        /// </summary>
        /// <remarks>This is an expensive operations and should be run once. As well, it unpacks files on the
        /// file system and is not thread-safe.</remarks>
        public void Prepare()
        {
            var contentDirectories = new List<string>();

            // Add the store directory to the list of directories with artifact content
            contentDirectories.Add(_storeDirectory);

            // Add any directories created by unzipping the zips in the store to the list of artifact directories
            contentDirectories = unpackZips(_storeDirectory);
            
            // Gather all filenames from the contentDirectories
            var fileNames = new List<string>();
            
            foreach (string contentDirectory in contentDirectories)
            {
               fileNames.AddRange(Directory.GetFiles(contentDirectory, "*.*", SearchOption.AllDirectories));
            }

            ArtifactFiles = fileNames;
            isPrepared = true;
        }


        // todo: public XsdSchemaSet GetSchemas
        // todo: public string[] GetArtifactFileNames(string pattern=null)

        private IEnumerable<string> _artifactFiles = null;

        public IEnumerable<string> ArtifactFiles 
        {
            get
            {
                ensurePrepared();
                return _artifactFiles;
            }

            private set
            {
                _artifactFiles = value;
            } 
        }

        public Resource ReadArtifact(ArtifactType type, Uri artifactId)
        {
            ensurePrepared();

            string artifactXml;

            if (artifactId == null) Error.ArgumentNull("artifactId");

            if (IsCoreArtifact(artifactId))
                artifactXml = findCoreArtifact(artifactId);
            else
                artifactXml = findUserArtifact(artifactId);

            return FhirParser.ParseResourceFromXml(artifactXml);                       
        }


        /// <summary>
        /// Given the Url for an artifact (e.g. http://hl7.org/fhir/profile/adversereaction), determines whether this is
        /// a core artifact that is pre-packaged in core files from the validation.zip
        /// </summary>
        /// <param name="artifactId">The location on the hl7.org repository of the core artifact</param>
        /// <returns></returns>
        public static bool IsCoreArtifact(Uri artifactId)
        {
            if(artifactId == null) throw Error.ArgumentNull("artifactId");

            var normalized = artifactId.ToString().ToLower();

            //return normalized.StartsWith(CORE_SPEC_PROFILE_URI_PREFIX) || normalized.StartsWith(CORE_SPEC_CONFORMANCE_URI_PREFIX) ||
            //        normalized.StartsWith(CORE_SPEC_CONCEPTMAP_URI_PREFIX) || normalized.StartsWith(CORE_SPEC_NAMESPACE_URI_PREFIX) ||
            //        normalized.StartsWith(CORE_SPEC_VS_URI_PREFIX) || normalized.StartsWith(CORE_SPEC_V2VS_URI_PREFIX) ||
            //        normalized.StartsWith(CORE_SPEC_V3VS_URI_PREFIX);

            return normalized.StartsWith(CORE_SPEC_URI_PREFIX);
        }

        private string findCoreArtifact(Uri artifactId)
        {
            // This is what the entry.id looks like in the profiles-resources.xml and profiles-types.xml
            // <id>http://hl7.org/fhir/profile/adversereaction</id> (a resource)
            // or <id>http://hl7.org/fhir/profile/period</id>

            // A v2 valueset's entry.id is buggy, so instead, look at ValueSet's identifier in v2-tables.xml
            // <id>http://hl7.org/fhir/v2/vs/0001</id> or
            // <id>http://hl7.org/fhir/v2/vs/0006/2.1</id>

            // A v3 valueset's entry.id is buggy too, so instead, look at ValueSet's identifier in v3-codesystems.xml
            // <id>http://hl7.org/fhir/v3/vs/AcknowledgementCondition</id>
            
            // FHIR's valuesets entry.id mostly look like
            // <id>http://hl7.org/fhir/vs/reactionSeverity</id>
            // but there are some alternatives too, so better use this file as a default

            var normalized = artifactId.ToString().ToLower();

            if (normalized.StartsWith(CORE_SPEC_PROFILE_URI_PREFIX))
                return readFromBundle(artifactId, "profiles-types.xml", "profiles-resources.xml");
            else if (normalized.StartsWith(CORE_SPEC_CONFORMANCE_URI_PREFIX))
                return readFromBundle(artifactId, "profiles-resources.xml");
            else if (normalized.StartsWith(CORE_SPEC_CONCEPTMAP_URI_PREFIX))
                throw Error.NotImplemented("Don't know where to locate core ConceptMaps, so this feature has not yet been implemented");
            else if (normalized.StartsWith(CORE_SPEC_NAMESPACE_URI_PREFIX))
                throw Error.NotImplemented("Namespaces are a DSTU2 feature, so this feature has not yet been implemented");
            else if (normalized.StartsWith(CORE_SPEC_VS_URI_PREFIX))
                return readFromBundle(artifactId, "valuesets.xml");
            else if (normalized.StartsWith(CORE_SPEC_V2VS_URI_PREFIX))
                return readFromBundle(artifactId, "v2-tables.xml");
            else if (normalized.StartsWith(CORE_SPEC_V3VS_URI_PREFIX))
                return readFromBundle(artifactId, "v3-codesystems.xml");
            else
                throw Error.NotImplemented("Url {0} was recognized as a core artifact, but I don't know where to locate it within validation.zip", normalized);
        }



        private string findUserArtifact(Uri artifactId)
        {
            // Locate a file that has the same name as the 'logical' id from the uri
            var fileName = new ResourceIdentity(artifactId).Id;

            if (fileName == null) throw Error.Argument("The artifactId {0} is not parseable as a normal http based REST endpoint with a logical id", artifactId.ToString());

            // Return the contents of the file, since there's no logical id inside the data of a simple resource file
            return File.ReadAllText(fileName);
        }



        private string readFromBundle(Uri artifactId, params string[] fileNames)
        {
            throw new NotImplementedException();
        }


        private void ensurePrepared()
        {
            if (!isPrepared) Prepare();
        }



        private static readonly string CACHEPATH = Path.Combine(Path.GetTempPath(), "FhirArtifactCache");


        private static List<string> unpackZips(string directory)
        {
            var zipFiles = Directory.GetFiles(directory, "*.zip");
            var result = new List<string>();

            foreach (var zipFile in zipFiles)
                result.Add(unzipArchive(zipFile).FullName);

            return result;
        }

        private static DirectoryInfo unzipArchive(string filename)
        {
            var cachedZipDir = getCachedZipDirectory(filename,File.GetLastWriteTimeUtc(filename));

            // If we have up-to-date unpacked files, do nothing
            if( cachedZipDir != null ) return cachedZipDir;

            // Cached dir does not exist or is too old, create a fresh new one
            var output = makeCleanCachedZipDirectory(filename);

            using (var zipfile = ZipFile.Read(filename))
            {
                zipfile.ExtractAll(output.FullName);
            }

            return output;
        }


        /// <summary>
        /// Ensures the cache directory contains a subdirectory matching the specified filename 
        /// (which is normally the name of the zip archive) -> you will get one directory per zip file
        /// with the name of the zipfile in the cache directory. If the directory already exists
        /// it will be deleted and recreated, to make sure it is empty
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private static DirectoryInfo makeCleanCachedZipDirectory(string filename)
        {
            var dir = getCacheDirectory();
            var cacheDirPath = Path.Combine(dir.FullName, filename);

            if (Directory.Exists(cacheDirPath)) Directory.Delete(cacheDirPath, recursive: true);

            return Directory.CreateDirectory(cacheDirPath);
        }


        /// <summary>
        /// Locates a zip-related directory within the cache directory that is as recent
        /// (or more recent) than the given baseline date
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="baseline"></param>
        /// <returns></returns>
        private static DirectoryInfo getCachedZipDirectory(string filename, DateTime baseline)
        {
            var dir = getCacheDirectory();
            var cacheDirPath = Path.Combine(dir.FullName,filename);

            var dirInfo = new DirectoryInfo(cacheDirPath);
            if( !dirInfo.Exists ) return null;

            if( dirInfo.CreationTimeUtc >= baseline.ToUniversalTime() )
                return dirInfo;
            else
                return null;
        }
     

        /// <summary>
        /// Gets the cache directory, or creates it if it did not exist
        /// </summary>
        /// <returns></returns>
        private static DirectoryInfo getCacheDirectory()
        {
            if (!Directory.Exists(CACHEPATH)) 
                return Directory.CreateDirectory(CACHEPATH);

            return new DirectoryInfo(CACHEPATH);
        }

        /// <summary>
        /// Remove the cache directory
        /// </summary>
        /// <returns></returns>
        private static DirectoryInfo clearCache()
        {
            if (Directory.Exists(CACHEPATH)) Directory.Delete(CACHEPATH, recursive: true);

            return getCacheDirectory();
        }    
    }


    public enum ArtifactType
    {
        Profile,
        ValueSet,
        Conformance,
        Namespace,
        ConceptMap
    }
}
