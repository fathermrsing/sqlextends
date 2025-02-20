using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace sqlextends
{
    public class JsonTableHelper
    {
        private int count = 0;
        public void FillRowFromJson(Object token, out SqlString path, out SqlString value, out SqlString type, out SqlBoolean hasvalues, out SqlInt32 index)
        {
            JToken item = (JToken)token;
            path = item.Path;
            type = item.Type.ToString();
            hasvalues = item.HasValues;
            value = item.ToString();
            index = count;
            count++;
        }
    }

    public class Md5Class
    {
        

        [Microsoft.SqlServer.Server.SqlProcedure]
        public static void HashString(SqlString value, out SqlString result)
        {
            string str = value.ToString().Trim();
            HashAlgorithm mhash = mhash = new MD5CryptoServiceProvider();
            byte[] bytValue = System.Text.Encoding.UTF8.GetBytes(str);
            byte[] bytHash = mhash.ComputeHash(bytValue);
            mhash.Clear();

            StringBuilder sBuilder = new StringBuilder();
            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < bytHash.Length; i++)
            {
                sBuilder.Append(bytHash[i].ToString("x2"));
            }
            // Return the hexadecimal string.
            result = sBuilder.ToString();
        }

        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlString FnHashString(SqlString value)
        {
            string str = value.ToString().Trim();
            SqlString result = "";
            HashAlgorithm mhash = mhash = new MD5CryptoServiceProvider();
            byte[] bytValue = System.Text.Encoding.UTF8.GetBytes(str);
            byte[] bytHash = mhash.ComputeHash(bytValue);
            mhash.Clear();

            StringBuilder sBuilder = new StringBuilder();
            // Loop through each byte of the hashed data
            // and format each one as a hexadecimal string.
            for (int i = 0; i < bytHash.Length; i++)
            {
                sBuilder.Append(bytHash[i].ToString("x2"));
            }
            // Return the hexadecimal string.
            result = sBuilder.ToString();
            return result;
        }

        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlBoolean FnIsMatch(string input, string pattern)
        {
            return new SqlBoolean(Regex.IsMatch(input, pattern));
        }
        
        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlString FnMatchSub(string input, string pattern)
        {
            Match match = Regex.Match(input, pattern);
            SqlString result = match.Success ? match.Value : "";
            return result;
        }
        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlString FnLongMatchSub(string input, 
            [SqlFacet(MaxSize = -1)] string patternPart1 = "", 
            [SqlFacet(MaxSize = -1)] string patternPart2 = "", 
            [SqlFacet(MaxSize = -1)] string patternPart3 = "")
        {
            string pattern = patternPart1 + patternPart2 + patternPart3;

            // 检查正则表达式是否为空，如果为空，则不进行匹配，直接返回空字符串
            if (string.IsNullOrEmpty(pattern))
            {
                return SqlString.Null; // 或者返回 SqlString("")，取决于您的需求
            }

            try
            {
                Match match = Regex.Match(input, pattern);
                return match.Success ? match.Value : SqlString.Null; // 或者返回 SqlString("")
            }
            catch (Exception ex)
            {
                // 处理正则表达式解析异常，例如记录日志或返回错误信息
                // 这里简单地返回空字符串
                return SqlString.Null; // 或者抛出异常
            }
        }
        
        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlString FnRegexReplace(string Input, string Pattern, string Replacement)
        {
            return new SqlString(Regex.Replace(Input, Pattern, Replacement));
        }

      

        [Microsoft.SqlServer.Server.SqlFunction]
        public static SqlString JsonValue(SqlString json, SqlString path)
        {
            try
            {
                JObject ja = (JObject)JsonConvert.DeserializeObject(json.Value);
                JToken token = ja.SelectToken(path.Value);

                return token.ToString();
            }
            catch
            {
                return null;
            }
        }

        [SqlFunction]
        public static SqlString JsonArrayValue(SqlString json, SqlInt32 rowindex, SqlString key)
        {
            JArray ja = (JArray)JsonConvert.DeserializeObject(json.Value);
            string re = ja[rowindex.Value][key.Value].ToString();
            return new SqlString(re);
        }

        //static int count = 0;
        public static void FillRowFromJson(Object token, out SqlString path, out SqlString value, out SqlString type, out SqlBoolean hasvalues)
        {
            JToken item = (JToken)token;
            path = item.Path;
            type = item.Type.ToString();
            hasvalues = item.HasValues;
            value = item.ToString();
     
        }

  
        [SqlFunction(FillRowMethodName = "FillRowFromJson", TableDefinition = "[path] nvarchar(max), [value] nvarchar(max), [type] nvarchar(max), hasvalues bit")]
        public static IEnumerable JsonTable(SqlString json, SqlString path)
        {
            ArrayList TokenCollection = new ArrayList();
            //count = 0;

            try
            {
                JObject ja = (JObject)JsonConvert.DeserializeObject(json.Value);
                IEnumerable<JToken> tokens = ja.SelectTokens(path.Value);

                foreach (JToken token in tokens)
                {
                    if (token.Type == JTokenType.Object || token.Type == JTokenType.Array)
                    {
                        foreach (JToken item in token.Children<JToken>())
                        {
                            TokenCollection.Add(item);
                        }
                    }
                    else
                    {
                        TokenCollection.Add(token);
                    }
                }

                return TokenCollection;

            }
            catch
            {
                return null;
            }
        }


    }



}