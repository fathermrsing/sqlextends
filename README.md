# sqlextends
> 参考项目： SqlClrJsonParser，但原来项目不支持sql server2008。
-  [x] 1、实现UTF-8表名的MD5方法sp_fnmd5Hash和存储过程sp_md5Hash，以便通过TSQL调用。
-  [x] 2、实现json方法：通过path查询json的key值(JsonValue)、json换回表(JsonTable）、数组json中获取指定第几个json的指定key值(JsonArrayValue)
-  [x] 3、正则表达式方法：替换方法FnRegexReplace、匹配方法FnIsMatch、匹配子串方法FnRegexMatchSub

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

USE yourdatabase;

-- 获取'yourdatabase' 数据库的当前所有者
DECLARE @currentOwner nvarchar(100);
SELECT @currentOwner = SUSER_SNAME(owner_sid) FROM sys.databases WHERE name = 'master';

select @currentOwner
-- 将数据库'yourdatabase' 的所有者更改为'sa'（或其他合适的登录名）
ALTER AUTHORIZATION ON DATABASE::yourdatabase TO sa;


USE yourdatabase;
ALTER DATABASE yourdatabase SET TRUSTWORTHY ON;

IF NOT EXISTS (SELECT * FROM sys.assemblies WHERE name = 'System_Runtime_Serialization')
	CREATE ASSEMBLY System_Runtime_Serialization FROM 'C:\Windows\Microsoft.NET\Framework\v3.0\Windows Communication Foundation\System.Runtime.Serialization.dll'
	WITH PERMISSION_SET = UNSAFE
GO

/*如果无法注册C:\Windows\Microsoft.NET\Framework\v3.0\Windows Communication Foundation\System.Runtime.Serialization.dll，可能是因为安装了高版本的.net,尝试：*/
IF NOT EXISTS (SELECT * FROM sys.assemblies WHERE name = 'System_Runtime_Serialization')
	CREATE ASSEMBLY System_Runtime_Serialization FROM 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Runtime.Serialization.dll'
	WITH PERMISSION_SET = UNSAFE
GO



IF NOT EXISTS (SELECT * FROM sys.assemblies WHERE name = 'Newtonsoft.Json')
	CREATE ASSEMBLY [Newtonsoft.Json]
	FROM 'C:\Program Files\Microsoft SQL Server\Newtonsoft.Json.dll'
	WITH PERMISSION_SET = UNSAFE
GO

--drop assembly all_my_sqlextends
create assembly all_my_sqlextends
from 'C:\Program Files\Microsoft SQL Server\sqlextends.dll'
go

--- md5 tools
if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[sp_md5Hash]') AND TYPE = 'PC'))
drop procedure [DBO].sp_md5Hash
go
create procedure dbo.sp_md5Hash (
@value nvarchar(max), 
@return nvarchar(max) output
) 
as 
external name [all_my_sqlextends].[sqlextends.Md5Class].HashString
go

if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[sp_fnmd5Hash]') AND TYPE = 'FS'))
drop function [DBO].sp_fnmd5Hash
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
if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[FnIsMatch]') AND TYPE = 'FS'))
drop function [DBO].[FnIsMatch]
go
CREATE FUNCTION [dbo].[FnIsMatch]
(@Input NVARCHAR (4000), @Pattern NVARCHAR (4000))
RETURNS BIT
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[FnIsMatch]
 
GO
if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[FnRegexMatchSub]') AND TYPE = 'FS'))
drop function [DBO].[FnRegexMatchSub]
go

CREATE FUNCTION [dbo].[FnRegexMatchSub]
(@Input NVARCHAR (4000), @Pattern NVARCHAR (max))
RETURNS NVARCHAR (4000)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[FnMatchSub] 
GO

if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[FnRegexLongMatchSub]') AND TYPE = 'FS'))
drop function [DBO].[FnRegexLongMatchSub]
go
CREATE FUNCTION [dbo].[FnRegexLongMatchSub]
(@Input NVARCHAR (4000), @Pattern1 NVARCHAR (max),@Pattern2 NVARCHAR (max),@Pattern3 NVARCHAR (max))
RETURNS NVARCHAR (4000)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].FnLongMatchSub

GO
if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[FnRegexReplace]') AND TYPE = 'FS'))
drop function [DBO].[FnRegexReplace]
go
CREATE FUNCTION [dbo].[FnRegexReplace]
(@Input NVARCHAR (4000), @Pattern NVARCHAR (4000), @Replacement NVARCHAR (4000))
RETURNS NVARCHAR (4000)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[FnRegexReplace]
 
GO	
select dbo.FnRegexReplace('A20智乐方绵阳火炬北路店','[\u4e00-\u9fa5]','')

 -------- json function
 
 GO
if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[JsonValue]') AND TYPE = 'FS'))
drop function [DBO].[JsonValue]
go
CREATE FUNCTION [dbo].[JsonValue]
(@json NVARCHAR (MAX), @path NVARCHAR (MAX))
RETURNS NVARCHAR (MAX)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[JsonValue]


GO
if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[JsonArrayValue]') AND TYPE = 'FS'))
drop function [DBO].[JsonArrayValue]
go
CREATE FUNCTION [dbo].[JsonArrayValue]
(@json NVARCHAR (MAX), @rowindex INT, @key NVARCHAR (MAX))
RETURNS NVARCHAR (MAX)
AS
 EXTERNAL NAME [all_my_sqlextends].[sqlextends.Md5Class].[JsonArrayValue]
 
GO

--test
declare @json varchar(max) = '
[{a:1},{b:3},{a:2}]'
select dbo.JsonArrayValue(@json,1,'b')

GO

if( exists(SELECT * FROM SYS.OBJECTS WHERE OBJECT_ID = OBJECT_ID('[DBO].[JsonTable]') AND TYPE = 'FT'))
drop function [DBO].[JsonTable]
go

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
	dbo.JsonValue([value], '$.a') AS [a], 
	dbo.JsonValue([value], '$.b') AS [b]
 from [dbo].[JsonTable]('{"key":"3","value":"test","arr":[{"a":1,"b":2},{"a":"3","b":"5"}]}','$')
 
select dbo.[FnRegexMatchSub]('电话：13784056631，Apple iPad Air 2024款11英寸WiFi版，购买日期：年月日' ,'\b1\d{10}\b')

```

