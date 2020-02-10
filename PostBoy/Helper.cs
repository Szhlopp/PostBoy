using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PostBoy
{
    public static class Helper
    {

        public static void AddRouteHeader(StringBuilder SB)
        {
            SB.AppendLine($"const express = require(\"express\");\r\nconst router = express.Router();\r\nconst uuidv4 = require('uuid/v4');\r\nconst cryptoRandomString = require('crypto-random-string');\r\nconst moment = require('moment');\r\nconst xml = require('xml');\r\n");
        }

        public static Tuple<string, List<string>, List<string>, bool> ParsePMCAndENV(this JToken itemToken, Dictionary<string, string> ENVkeys)
        {
            //Request URL
            var requestURL = string.Join("/", itemToken.SelectToken("request.url.path").asJArray().Select(f => (string)f));
            ENVkeys.Keys.ToList().ForEach(f => requestURL = requestURL.Replace($"{{{{{f}}}}}", ENVkeys[f]));
            Regex rgxKeys = new Regex(@"{{\$?(.+)}}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline);
            var rgxMatches = rgxKeys.Matches(requestURL);
            requestURL = rgxKeys.Replace(requestURL, @":$1");

            //Params
            //if (rgxMatches.Count > 0) System.Diagnostics.Debugger.Break();
            var Params = rgxMatches.ToList().Select(m => $"var param_{m.Groups[1].Value.Trim()} = req.params['{m.Groups[1].Value}'];").ToList();


            //Headers
            var headerToken = itemToken.SelectToken("response[0].header").asJArray().ToList();
            var Headers = headerToken.Select(h => $"res.set('{h.SelectTokenValueOrDefault("key")}', '{h.SelectTokenValueOrDefault("value")}');").ToList();

            var isJson = headerToken.Any(h => h.SelectTokenValueOrDefault("key", "nokey").ToString().Equals("content-type", StringComparison.OrdinalIgnoreCase) && h.SelectTokenValueOrDefault("value", "text").ToString().Contains("json"));

            return new Tuple<string, List<string>, List<string>, bool>(requestURL, Params, Headers, isJson);

        }

        public static object SelectTokenValueOrDefault(this JToken token, string path, object defaultValue = null)
        {
            JToken sel = token.SelectToken(path);
            if (sel?.GetType() == (typeof(JValue))) return ((JValue)sel).Value ?? defaultValue;
            else return defaultValue;
        }

        public static JArray asJArray(this JToken? token)
        {
            if (token != null && token.GetType() == typeof(JArray)) return (JArray)token;
            else return new JArray();
        }

        public static IDictionary<string, object> ToDictionary(this JObject obj)
        {
            var result = obj.ToObject<Dictionary<string, object>>();

            var JObjectKeys = (from r in result
                               let key = r.Key
                               let value = r.Value
                               where value.GetType() == typeof(JObject)
                               select key).ToList();

            var JArrayKeys = (from r in result
                              let key = r.Key
                              let value = r.Value
                              where value.GetType() == typeof(JArray)
                              select key).ToList();

            JArrayKeys.ForEach(key => result[key] = ((JArray)result[key]).Values().Select(x => ((JValue)x).Value).ToArray());
            JObjectKeys.ForEach(key => result[key] = ToDictionary(result[key] as JObject));

            return result;
        }

    }
}
