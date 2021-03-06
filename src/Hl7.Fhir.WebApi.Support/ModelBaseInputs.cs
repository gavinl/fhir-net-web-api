/* 
 * Copyright (c) 2017+ brianpos, Firely and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://github.com/ewoutkramer/fhir-net-api/blob/master/LICENSE
 */

using System;
using System.Security.Principal;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

namespace Hl7.Fhir.WebApi
{
    public class SystemModelBaseInputs<TSP> : ModelBaseInputs<TSP>
    {
        /// <summary>
        /// Constructor to create the Model inputs and set all the properties
        /// (Done as constructor with private setters so that when a new property 
        /// is added we don't miss places that need to populate it)
        /// </summary>
        /// <param name="baseUri">The Base URI is the actual FHIR URL used for this request, will end with /fhir/</param>
        /// <param name="serviceProvider"></param>
        public SystemModelBaseInputs(
            Uri baseUri,
            TSP serviceProvider)
            : base(GetSystemPrincipal(), null, null, null, baseUri, null, null, serviceProvider)
        {
        }
    }

    public class ModelBaseInputs<TSP>
    {
        /// <summary>
        /// Constructor to create the Model inputs and set all the properties
        /// (Done as constructor with private setters so that when a new property 
        /// is added we don't miss places that need to populate it)
        /// </summary>
        /// <param name="user"></param>
        /// <param name="clientCertificate"></param>
        /// <param name="httpMethod"></param>
        /// <param name="requestUri"></param>
        /// <param name="baseUri">The Base URI is the actual FHIR url used for this request, will end with /fhir/</param>
        /// <param name="x_api_key"></param>
        /// <param name="headers"></param>
        /// <param name="serviceProvider"></param>
        public ModelBaseInputs(
            IPrincipal user,
            X509Certificate2 clientCertificate,
            string httpMethod,
            Uri requestUri,
            Uri baseUri, 
            string x_api_key,
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers,
            TSP serviceProvider)
        {
            this.User = user;
            this.ClientCertificate = clientCertificate;
            this.HttpMethod = httpMethod;
            this.RequestUri = requestUri;
            this.BaseUri = baseUri;
            this.X_Api_Key = x_api_key;
            this.Headers = headers;
            this.ServiceProvider = serviceProvider;
        }

        /// <summary>
        /// The authenticated user name from the HttpContext
        /// </summary>
        public IPrincipal User { get; private set; }

        /// <summary>
        /// The certificate used to connect with the API
        /// </summary>
        public X509Certificate2 ClientCertificate { get; private set; }

        /// <summary>
        /// The CorrelationId of the request
        /// </summary>
        public string X_CorelationId { get; set; }

        /// <summary>
        /// The Http method e.g. GET
        /// </summary>
        public string HttpMethod { get; private set; }

        /// <summary>
        /// The request URI
        /// </summary>
        public Uri RequestUri { get; private set; }

        /// <summary>
        /// The request URI
        /// </summary>
        public Uri BaseUri { get; private set; }

        // Header properties that we care about
        // ETag
        // RequestedFormat (propagated from accept of format parameter)
        // Id-Modified-Since
        // If-None-Match
        // If-None-Exist
        // If-Match
        // Prefer (return=minimal or return=representation)

        /// <summary>
        /// The x-api-key from the headers of the request
        /// </summary>
        public string X_Api_Key { get; private set; }

        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> Headers { get; private set; }

        /// <summary>
        /// Access to the Dependency Injector
        /// </summary>
        public TSP ServiceProvider { get; private set; }

        private static IPrincipal _systemPrincipal;
        /// <summary>
        /// The System Principal is used when creating records on behalf of the server
        /// </summary>
        /// <returns></returns>
        public static IPrincipal GetSystemPrincipal()
        {
            if (_systemPrincipal == null)
            {
                _systemPrincipal = new GenericPrincipal(new GenericIdentity("System"), null);
            }
            return _systemPrincipal;
        }
    }
}