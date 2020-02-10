using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PostBoy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hi! My name is PostBoy. My goal?? Replace a collection of resource requests with a node server. And do it faster and cheaper than a grown up version of myself...\r\nPlease set the first argument to the main Collection Export (v2.1)\r\nThe second optional argument can be the envionment export file\r\nThe third it optional, if '1', will generate singular module and ignore folders");

            //App Template
            string NodeappTemplate = "";
            var resourceStream = Assembly.GetEntryAssembly().GetManifestResourceStream("PostBoy.NodeAppTemplate.js");
            using (var reader = new StreamReader(resourceStream, Encoding.UTF8)) NodeappTemplate = reader.ReadToEnd();

            int StartPort = 3001;

            //Zip base
            var resourceStreamNodeServer = Assembly.GetEntryAssembly().GetManifestResourceStream("PostBoy.NodeServer.zip");
            

            //ENVItems
            StringBuilder EnvironmentTS = new StringBuilder();

            args = new string[] { @"C:\Users\szhlo\Dropbox\WORK\CoreSite\Projects\Mule393Upgrade\UpgradeEfforts\NodeJS_API.postman_collectionwFolders.json", @"C:\Users\szhlo\Dropbox\WORK\CoreSite\Projects\Mule393Upgrade\UpgradeEfforts\APIMocks_CS.postman_environment.json" };

            if (args.Length > 0)
            {
                //Collection
                var PMC = JObject.Parse(File.ReadAllText(args[0]));
                
                //ENV
                Dictionary<string, string> ENVKeys = new Dictionary<string, string>();
                if ((args.Length >= 2 && File.Exists(args[1])))
                {
                    //Try to get them
                    JArray Values = JObject.Parse(File.ReadAllText(args[1])).SelectToken("values").asJArray();
                    foreach (var kvp in Values) ENVKeys.Add(kvp.Value<string>("key"), kvp.Value<string>("value"));

                }

                bool GenerateSingleModule = false;
                if (args.Length >= 3 && args[2] == "1") GenerateSingleModule = true;

                //Get Paths
                var basePath = Path.Combine(Path.GetDirectoryName(args[0]), "NodeServer");
                if (Directory.Exists(basePath)) Directory.CreateDirectory(basePath); //Directory.Delete(basePath, true);


                //Extract
                using (var nodezip = new ZipArchive(resourceStreamNodeServer))
                {
                    nodezip.ExtractToDirectory(basePath, true);
                }


                //Apps path
                var AppsPath = Path.Combine(basePath, "server", "apps");
                if (!Directory.Exists(AppsPath)) Directory.CreateDirectory(AppsPath);


                //Get folders in PMC
                var AppFolders = PMC["item"].asJArray().ToList().Where(f => f["request"] == null).ToList();
                if (AppFolders.Count > 0 && !GenerateSingleModule)
                {

                    foreach (var app in AppFolders)
                    {
                        //Get Name
                        string AppName = app["name"].ToString();

                        //Create local template
                        foreach (var c in Path.GetInvalidFileNameChars()) { AppName = AppName.Replace(c, '-'); }
                        string LocalTemplate = NodeappTemplate.Replace("###RoutefileName###", AppName);

                        var appPath = Path.Combine(AppsPath, AppName);
                        if (!Directory.Exists(appPath)) Directory.CreateDirectory(appPath);

                        StringBuilder SBRouteFile = new StringBuilder();
                        Helper.AddRouteHeader(SBRouteFile);

                        var Resources = PMC.SelectTokens($"item[?(@.name == '{app["name"].ToString()}')]..item[?(@.request)]").ToArray();
                        foreach (var item in Resources)
                        {
                            //res.set('Content-Type', 'text/xml');
                            var tpl = item.ParsePMCAndENV(ENVKeys);
                            SBRouteFile.AppendLine($"//{item.SelectTokenValueOrDefault("name", "NoName?")}\r\nrouter.{item.SelectTokenValueOrDefault("request.method", "get").ToString().ToLower()}(\"/{tpl.Item1}\", (req, res) => {{ \r\n  res.status({(new JArray(item.SelectTokens("response..code")).First?.ToString() ?? "200")});{(tpl.Item2.Count > 0 ? "\r\n  " + string.Join("\r\n  ", tpl.Item2.ToArray()) : "")}{(tpl.Item3.Count > 0 ? "\r\n  " + string.Join("\r\n  ", tpl.Item3.ToArray()) : "")}\r\n  {(tpl.Item4 ? $"res.json({(new JArray(item.SelectTokens("response[0].body")).First?.ToString().Replace("\"{{$guid}}\"", "uuidv4()") ?? "{ }")});\r\n" : $"res.send('{(new JArray(item.SelectTokens("response[0].body")).First?.ToString().Replace("'", "\\'").Replace("\"{{$guid}}\"", "uuidv4()") ?? "{ }")}');\r\n")}}});\r\n");
                        }

                        SBRouteFile.AppendLine($"\r\nmodule.exports = router;");

                        //Add environment item
                        EnvironmentTS.AppendLine($"environment['{AppName}'] = {{}};\r\nenvironment['{AppName}'].port = process.env.PORT || \"{StartPort++}\";\r\nenvironment['{AppName}'].url = \"\";\r\n\r\n");

                        //File write
                        File.WriteAllText(Path.Combine(appPath, $"{AppName}Routes.js"), SBRouteFile.ToString().Replace("{{$guid}}", "uuidv4()"));
                        File.WriteAllText(Path.Combine(appPath, $"{AppName}App.js"), NodeappTemplate.Replace("###RoutefileName###", AppName));

                    }

                   
                }
                else
                {
                    //single, without folders
                    var CollectionName = PMC.SelectToken("info")?.Value<string>("name") ?? "";
                    foreach (var c in Path.GetInvalidFileNameChars()) { CollectionName = CollectionName.Replace(c, '-'); }

                    var appPath = Path.Combine(AppsPath, CollectionName);
                    if (!Directory.Exists(appPath)) Directory.CreateDirectory(appPath);

                    StringBuilder SBRouteFile = new StringBuilder();
                    Helper.AddRouteHeader(SBRouteFile);

                    var Resources = PMC.SelectTokens("..item[?(@.request)]").ToList();
                    foreach (var item in Resources)
                    {
                        //res.set('Content-Type', 'text/xml');
                        var tpl = item.ParsePMCAndENV(ENVKeys);
                        SBRouteFile.AppendLine($"//{item.SelectTokenValueOrDefault("name", "NoName?")}\r\nrouter.{item.SelectTokenValueOrDefault("request.method", "get").ToString().ToLower()}(\"/{tpl.Item1}\", (req, res) => {{ \r\n  res.status({(new JArray(item.SelectTokens("response..code")).First?.ToString() ?? "200")});{(tpl.Item2.Count > 0 ? "\r\n  " + string.Join("\r\n  ", tpl.Item2.ToArray()) : "")}{(tpl.Item3.Count > 0 ? "\r\n  " + string.Join("\r\n  ", tpl.Item3.ToArray()) : "")}\r\n  {(tpl.Item4 ? $"res.json({(new JArray(item.SelectTokens("response[0].body")).First?.ToString().Replace("\"{{$guid}}\"", "uuidv4()") ?? "{ }")});\r\n" : $"res.send('{(new JArray(item.SelectTokens("response[0].body")).First?.ToString().Replace("'", "\\'").Replace("\"{{$guid}}\"", "uuidv4()") ?? "{ }")}');\r\n")}}});\r\n");
                    }

                    SBRouteFile.AppendLine($"\r\nmodule.exports = router;");

                    //Add environment item
                    EnvironmentTS.AppendLine($"environment['{CollectionName}'] = {{}};\r\nenvironment['{CollectionName}'].port = process.env.PORT || \"3002\";\r\nenvironment['{CollectionName}'].url = \"\";\r\n\r\n");


                    //File write
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[0]), $"{CollectionName}Routes.js"), SBRouteFile.ToString().Replace("{{$guid}}", "uuidv4()"));
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[0]), $"{CollectionName}App.js"), NodeappTemplate.Replace("###RoutefileName###", CollectionName));
                }


                //Write environment files items
                using (var FS = File.Open(Path.Combine(basePath, "server", "environment.js"), FileMode.OpenOrCreate))
                using (StreamReader SR = new StreamReader(FS))
                using (StreamWriter SW = new StreamWriter(FS))
                {
                    var NewEnvFile = SR.ReadToEnd().Replace("##ENVS##", EnvironmentTS.ToString());
                    FS.Seek(0, SeekOrigin.Begin);
                    //using (StreamWriter SW = new StreamWriter(FS))
                    SW.Write(NewEnvFile);
                }


            }
            else
            {
                Console.WriteLine($"Post boy requires a file. Give file please");
            }

            
        }

       
    }
}
