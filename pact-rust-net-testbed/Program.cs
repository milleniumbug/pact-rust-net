namespace Testbed
{
    using System;
    using System.Net.Http;
    using System.Text;
    using PactNet.Core;

    public class Program
    {
        private static readonly string pact = @"
{
  ""consumer"": {
    ""name"": ""user""
  },
  ""provider"": {
    ""name"": ""customer-service""
  },
  ""interactions"": [
    {
      ""description"": ""thing"",
      ""providerState"": ""there exists a customer with id of 23"",
      ""request"": {
        ""method"": ""get"",
        ""path"": ""/customers/23""
      },
      ""response"": {
        ""status"": 200,
        ""headers"": {
          ""Content-Type"": ""application/json; charset=utf-8""
        },
        ""body"": {
          ""id"": 23,
          ""name"": ""Name Surname""
        }
      }
    },
    {
      ""description"": ""thing"",
      ""providerState"": ""not authorized"",
      ""request"": {
        ""method"": ""get"",
        ""path"": ""/customers/23"",
        ""headers"": {
        }
      },
      ""response"": {
        ""status"": 401,
        ""headers"": {
          ""Content-Type"": ""application/json; charset=utf-8""
        },
        ""body"": {
          ""error"": ""Not authorized""
        }
      }
    }
  ],
  ""metadata"": {
    ""pactSpecification"": {
      ""version"": ""2.0.0""
    }
  }
}";

        public static void Main()
        {
            using (var port = NativeRustFunctions.CreateMockServer(pact, 7000))
            {
                var c = new HttpClient();
                c.BaseAddress = new Uri($"http://localhost:{port.GetPortNumber()}");
                var r = c.GetStringAsync("/customers/23").Result;
                Console.WriteLine(r);
                Console.WriteLine(NativeRustFunctions.MockServerMismatches(port));
            }
        }
    }
}
