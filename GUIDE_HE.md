# מדריך קצר – Orbit Operations Platform

## מה מקבלים?

- `Backend/OperationsPlatform.Api` – ASP.NET Core 8 Web API ומנוע הפעולות.
- `Backend/OperationsPlatform.CSharpWorker` – Worker נפרד שמבצע קומפילציה והרצה של קוד C# באמצעות Roslyn.
- `Frontend` – Angular 12 עם Dashboard, קטלוג, מסכי ניהול והרצה, Builder, היסטוריה ולוגים.
- `docs/OperationsPlatform.postman_collection.json` – אוסף בקשות לדוגמה לבדיקת ה-API.

## 1. דרישות התקנה

1. SQL Server 2017 Developer Edition (כבר מותקן אצלך) ושירות Database Engine פועל.
2. .NET 8 SDK.
3. Node.js 14.21.3 ו-npm התואם לו. Angular 12 אינו תואם ל-Node 22.
4. מומלץ: Visual Studio 2022 ו-SQL Server Management Studio.

ב-Windows אפשר להתקין Node.js 14.21.3 באמצעות מתקין MSI הרשמי של Node.js או באמצעות nvm-windows. לאחר התקנת nvm-windows, פתח PowerShell כמנהל והריץ:

```powershell
nvm install 14.21.3 64
nvm use 14.21.3
node --version
```

הפקודה האחרונה צריכה להציג `v14.21.3`.

בדיקות מהירות ב-PowerShell:

```powershell
dotnet --list-sdks
node --version
npm --version
```

## 2. חיבור SQL Server

פתח את:

`Backend/OperationsPlatform.Api/appsettings.json`

עדכן את `ConnectionStrings:DefaultConnection` לפי ה-Server Name שמופיע בחלון ההתחברות של SSMS.

Default instance:

```text
Server=localhost;Database=OperationsPlatformDb;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;
```

Named instance לדוגמה:

```text
Server=localhost\SQLEXPRESS;Database=OperationsPlatformDb;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;
```

החיבור משתמש ב-Windows Authentication. לחשבון Windows שמפעיל את השרת צריכה להיות הרשאה ליצור Database. אם אתה משתמש ב-SQL Authentication, החלף את `Trusted_Connection=True` ב-`User Id=...;Password=...`.

## 3. הפעלה ראשונה

פתח שני חלונות PowerShell מתוך תיקיית הפרויקט.

**חלון ראשון – Backend:**

```powershell
cd .\OperationsPlatform
# If PowerShell blocks scripts in this window:
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\start-backend.ps1
```

הסקריפט מבצע Restore ו-Build לכל ה-Solution, כולל ה-CSharpWorker, ולאחר מכן מפעיל את ה-API.

כתובות:

- API: `http://localhost:5080`
- Swagger: `http://localhost:5080/swagger`
- Health: `http://localhost:5080/health`

**חלון שני – Frontend:**

```powershell
cd .\OperationsPlatform
# If PowerShell blocks scripts in this window:
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\start-frontend.ps1
```

הממשק זמין ב-`http://localhost:4200`.

בעלייה הראשונה Entity Framework מפעיל `EnsureCreatedAsync`, יוצר את `OperationsPlatformDb` ואת הטבלאות `Operations`, `OperationExecutions`, `ApplicationLogs`, ולאחר מכן Seed Data מוסיף פעולות לדוגמה. לא צריך להריץ SQL ידנית.

> `EnsureCreated` מיועד להקמה ראשונית פשוטה ואינו EF Migrations. אם משנים בעתיד את מבנה ה-Entities, יש לעבוד עם DB מקומי חדש או להעביר את הפרויקט ל-EF Migrations לפני שמירת נתונים שחשובים לך.

## 4. הגדרות חשובות

### Backend – `appsettings.json`

| הגדרה | מה היא עושה |
|---|---|
| `ConnectionStrings:DefaultConnection` | חיבור SQL Server |
| `Frontend:AllowedOrigins` | כתובות Angular שמותר להן לבצע קריאות CORS |
| `CSharpWorker:RelativeDllPath` | מיקום ה-Worker אחרי Build |
| `CSharpWorker:ExecutionTimeoutSeconds` | זמן מקסימלי לפעולת C# |
| `CSharpWorker:MaxSourceCodeLength` | מגבלת אורך קוד C# |
| `RestApi:DefaultTimeoutSeconds` | Timeout לקריאות REST |
| `RestApi:MaxResponseBytes` | גודל תשובה מקסימלי מ-API חיצוני |
| `RestApi:AllowPrivateNetworkTargets` | מאפשר קריאות לכתובות פנימיות; true מוגדר לתרגיל מקומי/ארגוני |
| `Execution:TimeZoneId` | אזור הזמן לספירת פעולות חודשית |
| `Execution:MaxInputLength` | אורך מקסימלי לכל שדה קלט |
| `Execution:MaxBuilderNodes` | מספר Nodes מרבי ב-Builder |
| `Logging:MaxBodyChars` | מספר תווים מרבי מגוף Request/Response הנשמר בלוג |

### Frontend – Angular environment

כתובת ה-API מוגדרת בקובץ:

`Frontend/src/environments/environment.ts`

```ts
apiBaseUrl: 'http://localhost:5080/api'
```

אם שינית את כתובת השרת, עדכן גם את `Frontend:AllowedOrigins` ב-`appsettings.json`. Theme נשמר בדפדפן ב-`localStorage`.

## 5. איך משתמשים בממשק?

1. **Overview** – מדדי שימוש, אחוז הצלחה והרצות אחרונות.
2. **Operations** – קטלוג פעולות. אפשר לחפש, לסנן, להריץ, לערוך, להשבית ולהוסיף פעולה.
3. **Create Operation** – בחירת Implementation Type: Built-in, Custom C# או REST API.
4. **Operation Builder** – יצירת Composite Operation באמצעות Nodes קיימים. ניתן לשנות את סדרם, לבחור Handler ולהעביר Output של Node קודם לשלב הבא.
5. **Execution History** – כל הרצה, כולל Inputs, Result, Status, Duration וגרסת הפעולה.
6. **Application Logs** – בקשות ותשובות נכנסות לשרת, קריאות REST יוצאות, Headers (עם Masking לערכים רגישים) ושגיאות.

## 6. דרכי יצירת פעולה

### Built-in Handler

בחר Handler קיים והגדר אותו באמצעות JSON. לדוגמה:

```json
{
  "handlerKey": "string.concatenate",
  "separator": " - "
}
```

הוספת הגדרה חדשה המשתמשת ב-Handler קיים אינה דורשת שינוי קוד. כדי להוסיף אלגוריתם חדש שאינו בספריית ה-Handlers, יש להוסיף Handler ב-C# ולבנות מחדש.

### Custom C# באמצעות Roslyn

הקוד נשמר כטקסט ב-`ConfigurationJson.sourceCode`. החתימה הנדרשת:

```csharp
using System.Text.Json;

public static class CustomOperation
{
    public static object? Execute(
        string? fieldA,
        string? fieldB,
        JsonElement configuration)
    {
        return $"{fieldA}{fieldB}";
    }
}
```

אפשר לקרוא הגדרות נוספות דרך `configuration`. מסך הניהול כולל Validate Code לפני שמירה.

### REST API

הגדר URL, Method, Headers, Query Parameters, Body Template, Timeout ו-Authentication. אפשר להשתמש ב-`{{fieldA}}` וב-`{{fieldB}}` בתוך URL, Query Parameters ו-Body Template. דוגמת GET:

```json
{
  "httpMethod": "GET",
  "url": "https://api.example.org/lookup",
  "queryParameters": {
    "valueA": "{{fieldA}}",
    "valueB": "{{fieldB}}"
  },
  "headers": { "Accept": "application/json" },
  "timeoutSeconds": 15,
  "responseType": "Json"
}
```

### Operation Builder

ה-Builder מריץ Nodes לפי הסדר מלמעלה למטה. קלט Node יכול להיות:

- `$fieldA` או `$fieldB` – הקלט המקורי של הפעולה.
- `stepId.output` – תוצאה של Node קודם.
- `literal:text` – ערך קבוע.

בגרסה הנוכחית אין לולאות, הסתעפויות או Nodes מותנים. ה-Builder משתמש ב-Built-in handlers בלבד.

## 7. Weather Forecast

פעולת Weather Forecast מגיעה מוכנה עם Seed Data. היא משתמשת ב-Open-Meteo, מקבלת Latitude ו-Longitude ומחזירה JSON עם נתוני מזג אוויר. נדרש חיבור אינטרנט מהמחשב שבו פועל ה-Backend. אין צורך ב-API Key לשימוש החינמי הלא-מסחרי; הממשק מציג קרדיט ל-Open-Meteo.

## 8. בדיקת API חיצוני

ייבא ל-Postman את:

`docs/OperationsPlatform.postman_collection.json`

הגדר את `baseUrl` ל-`http://localhost:5080/api`. הבקשות כוללות קבלת רשימת פעולות ודוגמת Execute. אותו API משמש את Angular ואת אפליקציות חיצוניות.

## 9. אבטחה – חובה לקרוא

הפרויקט מיועד לתרגיל מקומי או לרשת ארגונית סגורה בלבד. אין לחשוף אותו לאינטרנט:

- אין Login או Authorization; כל מי שמגיע ל-API יכול לשנות פעולות, להגדיר URL חיצוני ולשמור קוד C#.
- Roslyn מבצע קומפילציה אך אינו Sandbox. ה-CSharpWorker הוא תהליך נפרד עם Timeout, אך רץ תחת אותו חשבון Windows ויכול לגשת למשאבים שהחשבון מורשה אליהם. השתמש רק בקוד מהימן.
- `AllowPrivateNetworkTargets=true` מאפשר קריאות REST לכתובות פנימיות. זה נדרש לאינטגרציות Intranet בתרגיל, אך יוצר סיכון SSRF.
- סודות Authentication נשמרים בטקסט ב-SQL המקומי, אך מוסתרים בתשובות GET של ה-API. בעריכת פעולה, אם לא מזינים סוד חדש ונשארים באותו סוג Authentication, הערך הקיים נשמר. כדי להסיר Authentication יש לבחור None. אין להשתמש במפתחות אמיתיים או במידע רגיש.
- הלוגים מבצעים Masking לכותרות ושדות סודיים נפוצים, אך אין לשמור מידע רגיש בגופי בקשות ותשובות.

## 10. פתרון תקלות

- **SQL connection failed:** בדוק את שם השרת/Instance ב-SSMS, ששירות SQL Server פועל ושהחשבון רשאי ליצור Database.
- **Worker not found:** בנה את כל `OperationsPlatform.sln`; אין לבנות רק DLL או להריץ Worker ללא קבצי ה-runtime שלו.
- **Angular package install fails:** ודא ש-Node הוא 14.15 ומעלה בתוך קו Node 14 (מומלץ 14.21.3), ולא Node 22.
- **CORS error:** ודא ש-Angular רץ ב-4200 ושה-Origin מופיע ב-`Frontend:AllowedOrigins`.
- **Weather fails:** בדוק חיבור אינטרנט מהמחשב של ה-Backend והזן Latitude/Longitude תקינים.
