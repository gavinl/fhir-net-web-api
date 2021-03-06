﻿using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;
using Hl7.Fhir.Utility;
using System.Threading.Tasks;
using System.Linq;

namespace Hl7.DemoFileSystemFhirServer
{
    public class DirectoryResourceService : Hl7.Fhir.WebApi.IFhirResourceServiceR4<IServiceProvider>
    {
        public ModelBaseInputs<IServiceProvider> RequestDetails { get; set; }

        public string ResourceName { get; set; }

        public Task<Resource> Create(Resource resource, string ifMatch, string ifNoneExist, DateTimeOffset? ifModifiedSince)
        {
            if (String.IsNullOrEmpty(resource.Id))
                resource.Id = Guid.NewGuid().ToFhirId();
            if (resource.Meta == null)
                resource.Meta = new Meta();
            resource.Meta.LastUpdated = DateTime.Now;
            string path = System.IO.Path.Combine(DirectorySystemService.Directory, $"{resource.TypeName}.{resource.Id}.{resource.Meta.VersionId}.xml");
            System.IO.File.WriteAllText(
                path,
                new Hl7.Fhir.Serialization.FhirXmlSerializer().SerializeToString(resource));
            resource.SetAnnotation<CreateOrUpate>(CreateOrUpate.Create);
            return System.Threading.Tasks.Task.FromResult(resource);
        }

        public Task<string> Delete(string id, string ifMatch)
        {
            string path = System.IO.Path.Combine(DirectorySystemService.Directory, $"{this.ResourceName}.{id}..xml");
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            return System.Threading.Tasks.Task.FromResult<string>(null);
        }

        public Task<Resource> Get(string resourceId, string VersionId, SummaryType summary)
        {
            string path = System.IO.Path.Combine(DirectorySystemService.Directory, $"{this.ResourceName}.{resourceId}.{VersionId}.xml");
            if (System.IO.File.Exists(path))
                return System.Threading.Tasks.Task.FromResult(new Fhir.Serialization.FhirXmlParser().Parse<Resource>(System.IO.File.ReadAllText(path)));
            throw new FhirServerException(System.Net.HttpStatusCode.Gone, "It might have been deleted!");
        }

        public Task<CapabilityStatement.ResourceComponent> GetRestResourceComponent()
        {
            throw new NotImplementedException();
        }

        public Task<Bundle> InstanceHistory(string ResourceId, DateTimeOffset? since, DateTimeOffset? Till, int? Count, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public Task<Resource> PerformOperation(string operation, Parameters operationParameters, SummaryType summary)
        {
             if (operation == "count-em")
            {
                var result = new OperationOutcome();
                result.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.Informational,
                    Severity = OperationOutcome.IssueSeverity.Information,
                    Details = new CodeableConcept(null, null, $"{ResourceName} resource instances: {System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory, $"{ResourceName}.*.xml").Count()}")
                });
                return System.Threading.Tasks.Task.FromResult<Resource>(result);
            }
            if (operation == "preferred-id")
            {
                // Test operation that isn't really anything just for a specific unit test
                NamingSystem ns = new NamingSystem() { Id = operationParameters.GetString("id") };
                return System.Threading.Tasks.Task.FromResult<Resource>(ns);
            }

            throw new NotImplementedException();
        }

        public Task<Resource> PerformOperation(string id, string operation, Parameters operationParameters, SummaryType summary)
        {
            throw new NotImplementedException();
        }

        public Task<Bundle> Search(IEnumerable<KeyValuePair<string, string>> parameters, int? Count, SummaryType summary)
        {
            // This is a Brute force search implementation - just scan all the files
            Bundle resource = new Bundle();
            if (resource.Meta == null)
                resource.Meta = new Meta();
            resource.Meta.LastUpdated = DateTime.Now;
            resource.Id = new Uri("urn:uuid:" + Guid.NewGuid().ToFhirId()).OriginalString;
            resource.Type = Bundle.BundleType.Searchset;
            resource.ResourceBase = RequestDetails.BaseUri;

            Dictionary<string, Resource> entries = new Dictionary<string, Resource>();
            var parser = new Hl7.Fhir.Serialization.FhirXmlParser();
            string filter = $"{ResourceName}.*.*.xml";
            var idparam = parameters.Where(kp => kp.Key == "_id");
            if (idparam.Any())
            {
                // Even though this is a trashy demo app, don't permit walking the file system
                filter = $"{ResourceName}.{idparam.First().Value.Replace("/", "")}.*.xml";
            }
            foreach (var filename in System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory, filter))
            {
                using (System.IO.FileStream stream = new System.IO.FileStream(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    var xr = Hl7.Fhir.Utility.SerializationUtil.XmlReaderFromStream(stream);
                    var resourceEntry = parser.Parse<Resource>(xr);
                    if (entries.ContainsKey(resourceEntry.Id))
                    {
                        if (String.Compare(entries[resourceEntry.Id].Meta.VersionId, resourceEntry.Meta.VersionId) < 0)
                            entries[resource.Id] = resourceEntry;
                    }
                    else
                        entries.Add(resourceEntry.Id, resourceEntry);
                }
            }
            foreach (var item in entries.Values)
            {
                resource.AddResourceEntry(item,
                    ResourceIdentity.Build(RequestDetails.BaseUri,
                        item.ResourceType.ToString(),
                        item.Id,
                        item.Meta.VersionId).OriginalString).Search = new Bundle.SearchComponent()
                        {
                            Mode = Bundle.SearchEntryMode.Include
                        };
            }
            resource.Total = resource.Entry.Count(e => e.Search.Mode == Bundle.SearchEntryMode.Include);
            if (Count.HasValue)
                resource.Entry = resource.Entry.Take(Count.Value).ToList();
            if (parameters.Count(p => p.Key != "_id") > 0)
            {
                var outcome = new OperationOutcome();
                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Severity = OperationOutcome.IssueSeverity.Warning,
                    Code = OperationOutcome.IssueType.NotSupported,
                    Details = new CodeableConcept() { Text = $"Unsupported search parameters used: {String.Join("&", parameters.Where(p => p.Key != "_id").Select(k => k.Key + "=" + k.Value))}" }
                });
                resource.AddResourceEntry(outcome,
                    new Uri("urn:uuid:" + Guid.NewGuid().ToFhirId()).OriginalString).Search = new Bundle.SearchComponent()
                    {
                        Mode = Bundle.SearchEntryMode.Outcome
                    };
            }

            return System.Threading.Tasks.Task.FromResult(resource);
        }

        public Task<Bundle> TypeHistory(DateTimeOffset? since, DateTimeOffset? Till, int? Count, SummaryType summary)
        {
            Bundle result = new Bundle();
            result.Meta = new Meta()
            {
                LastUpdated = DateTime.Now
            };
            result.Id = new Uri("urn:uuid:" + Guid.NewGuid().ToString("n")).OriginalString;
            result.Type = Bundle.BundleType.History;

            var parser = new Fhir.Serialization.FhirXmlParser();
            var files = System.IO.Directory.EnumerateFiles(DirectorySystemService.Directory, $"{ResourceName}.*.xml");
            foreach (var filename in files)
            {
                var resource = parser.Parse<Resource>(System.IO.File.ReadAllText(filename));
                result.AddResourceEntry(resource,
                    ResourceIdentity.Build(RequestDetails.BaseUri,
                        resource.ResourceType.ToString(),
                        resource.Id,
                        resource.Meta.VersionId).OriginalString);
            }
            result.Total = result.Entry.Count;

            // also need to set the page links

            return System.Threading.Tasks.Task.FromResult(result);
        }
    }
}
