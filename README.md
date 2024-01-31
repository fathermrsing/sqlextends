# sqlextends
1、编写工程

- 创建C#类库
- 代码

> 以sql server 2008 为例，.netframework 选择3.5
>
> Newtonsoft.json 8.0.0.0

```c#
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Data;
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
        public static SqlBoolean FnIsMatch(string Input, string Pattern)
        {
            return new SqlBoolean(Regex.IsMatch(Input, Pattern));
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

        [Microsoft.SqlServer.Server.SqlFunction]
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
```

- 编译生成dll，拷贝到数据库服务器上：如：D:\ebs2_deploy\sqlextends.dll

2、加载clr并声明

```sql
---开启所有服务器配置选项
EXEC sp_configure N'show advanced options', N'1' 
RECONFIGURE WITH OVERRIDE
--开启clr enabled 选项
EXEC sp_configure N'clr enabled', N'1'
RECONFIGURE WITH OVERRIDE 
--关闭所有服务器配置选项
EXEC sp_configure N'show advanced options', N'0' 
RECONFIGURE WITH OVERRIDE
--如果存在权限问题，执行下面一段脚本，注意，尽量创建临时库来实现
--alter database [master] set TRUSTWORTHY on
--EXEC sp_changedbowner 'sa'
GO 


--------------------
--注意建立一个无关紧要的库来做

USE OARef;

-- 获取'OARef' 数据库的当前所有者
DECLARE @currentOwner nvarchar(100);
SELECT @currentOwner = SUSER_SNAME(owner_sid) FROM sys.databases WHERE name = 'master';

select @currentOwner
-- 将数据库'OARef' 的所有者更改为'sa'（或其他合适的登录名）
ALTER AUTHORIZATION ON DATABASE::OARef TO sa;


USE OARef;
ALTER DATABASE OARef SET TRUSTWORTHY ON;

IF NOT EXISTS (SELECT * FROM sys.assemblies WHERE name = 'System_Runtime_Serialization')
	CREATE ASSEMBLY System_Runtime_Serialization FROM 'C:\Windows\Microsoft.NET\Framework\v3.0\Windows Communication Foundation\System.Runtime.Serialization.dll'
	WITH PERMISSION_SET = UNSAFE
GO

IF NOT EXISTS (SELECT * FROM sys.assemblies WHERE name = 'Newtonsoft.Json')
	CREATE ASSEMBLY [Newtonsoft.Json]
	FROM 'D:\ebs2_deploy\sqlextends\Newtonsoft.Json.dll'
	WITH PERMISSION_SET = UNSAFE
GO

--drop assembly all_my_sqlextends
create assembly all_my_sqlextends
from 'D:\ebs2_deploy\sqlextends\sqlextends.dll'
go

--- md5 tools
create procedure dbo.sp_md5Hash (
@value nvarchar(max), 
@return nvarchar(max) output
) 
as 
external name [all_my_sqlextends].[sqlextends.Md5Class].HashString
go

create function sp_fnmd5Hash (@value nvarchar(max)) RETURNS nvarchar(max) 
as 
	external name [all_my_sqlextends].[sqlextends.Md5Class].FnHashString;
go
  
declare @res nvarchar(max),
	@rawtext nvarchar(100) = 'SASDFASDFASDFAasdf7F225974-8584-4774-BA93-75D5D8D5F257'

exec dbo.sp_md5Hash @rawtext, @res output
select @res, dbo.sp_fnmd5Hash(@rawtext)


-----Regex tools
GO
CREATE FUNCTION [dbo].[FnIsMatch]
(@Input NVARCHAR (4000), @Pattern NVARCHAR (4000))
RETURNS BIT
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[FnIsMatch]
 
GO
CREATE FUNCTION [dbo].[FnRegexReplace]
(@Input NVARCHAR (4000), @Pattern NVARCHAR (4000), @Replacement NVARCHAR (4000))
RETURNS NVARCHAR (4000)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[FnRegexReplace]
 
GO	
select dbo.FnRegexReplace('A20智乐方绵阳火炬北路店','[\u4e00-\u9fa5]','')
 -------- json function
 
 GO
CREATE FUNCTION [dbo].[JsonValue]
(@json NVARCHAR (MAX), @path NVARCHAR (MAX))
RETURNS NVARCHAR (MAX)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[JsonValue]


 
GO
CREATE FUNCTION [dbo].[JsonArrayValue]
(@json NVARCHAR (MAX), @rowindex INT, @key NVARCHAR (MAX))
RETURNS NVARCHAR (MAX)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[JsonArrayValue]
 
GO
CREATE FUNCTION [dbo].[JsonTable]
(@json NVARCHAR (MAX), @path NVARCHAR (MAX))
RETURNS 
     TABLE (
        [path]      NVARCHAR (4000) NULL,
        [value]     NVARCHAR (MAX)  NULL,
        [type]      NVARCHAR (4000) NULL,
        [hasvalues] BIT             NULL)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[JsonTable]

go
select *,
	dbo.JsonValue([value], '$.key') AS [key],
	dbo.JsonValue([value], '$.a') AS [a]
, dbo.JsonValue([value], '$.b') AS [b]

 from [dbo].[JsonTable]('{"key":"3","value":"test","arr":[{"a":1,"b":2},{"a":"3","b":"5"}]}','$')
```

