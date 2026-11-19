# Orbit – Universal Operations Platform

פלטפורמת Operations גנרית המבוססת על ASP.NET Core 8, Entity Framework Core 8, SQL Server ו-Angular 12.

> הפרויקט הוכן לפי אפיון Gaia ולפי ההחלטות שסוכמו: כל פעולה מקבלת Field A ו-Field B, כל ההרצות נעשות בשרת, וכל API משמש גם את Angular וגם לקוחות חיצוניים.

## יכולות בפרויקט

- קטלוג פעולות דינמי הנשמר ב-SQL Server.
- Built-in handlers: פעולות String ו-Integer.
- Custom C#: קוד C# נשמר כטקסט בתוך `ConfigurationJson`, נבדק ומקולמפל באמצעות Roslyn בתהליך Worker נפרד.
- REST API: מנוע HTTP גנרי, עם URL, Method, Headers, Query Parameters, Body, Timeout וסוג תשובה מוגדרים ב-JSON.
- Operation Builder: רצף ויזואלי של Nodes שמפעילים Built-in handlers קיימים ומעבירים Outputs בין שלבים.
- Angular UI: Dashboard, Catalog, Create/Edit, Execute, Builder, History ו-Logs.
- Dark Theme ו-Light Theme.
- SQL history: שלוש הרצות אחרונות של אותה פעולה וספירת הפעלות מתחילת החודש המקומי (Israel Standard Time).
- SQL application logs: רישום Requests/Responses נכנסים ל-API, קריאות REST יוצאות, ושגיאות מערכת.
- Soft Delete: השבתת פעולה בלי למחוק את היסטוריית ההרצות שלה.
- Seed אוטומטי של פעולות לדוגמה, כולל Weather Forecast של Open-Meteo.

## דרישות סביבת פיתוח

### Backend

- .NET SDK 8.x.
- Visual Studio 2022 או CLI.
- SQL Server 2017 ומעלה. הפרויקט נבדק תכנונית מול SQL Server 2017 Developer Edition.

### Frontend

Angular 12.2 דורש Node.js `^12.14.0` או `^14.15.0`, TypeScript `>=4.2.3 <4.4.0` ו-RxJS 6. הפרויקט נועל TypeScript 4.3.5 ו-RxJS 6.6.7. מומלץ להשתמש ב-Node 14.21.3 בסביבת תרגיל זו. Angular 12 ו-Node 14 אינם נתמכים עוד כיום; הם נשמרו במכוון בהתאם לדרישה.

טבלת תאימות רשמית: https://angular.dev/reference/versions

## 1. הגדרת SQL Server

המהדורה שציינת – SQL Server 2017 Developer Edition – מתאימה לפרויקט.

פתח את `Backend/OperationsPlatform.Api/appsettings.json` ועדכן את `ConnectionStrings:DefaultConnection` לפי שם השרת שמופיע בחלון ההתחברות של SSMS.

### Default Instance

```text
Server=localhost;Database=OperationsPlatformDb;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;
```

### Named Instance – דוגמה בלבד

```text
Server=localhost\SQLEXPRESS;Database=OperationsPlatformDb;Trusted_Connection=True;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;
```

אם SQL Server מותקן תחת שם מחשב/Instance אחר, החלף רק את החלק `Server=...`.

אפשר להשתמש גם ב-SQL Authentication, לדוגמה:

```text
Server=localhost;Database=OperationsPlatformDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;
```

אל תשמור סיסמה אמיתית ב-Git. בסביבה מקומית עדיף להשתמש ב-.NET User Secrets או במשתני סביבה.

### יצירת בסיס הנתונים והטבלאות

באתחול הראשון השרת מפעיל `Database.EnsureCreatedAsync()` של Entity Framework Core. לכן, אם ה-Login/Windows account שמריץ את השרת מורשה ליצור Database, EF ייצור אוטומטית את `OperationsPlatformDb` ואת הטבלאות:

- `Operations`
- `OperationExecutions`
- `ApplicationLogs`

לא צריך להריץ SQL ידנית. לאחר יצירת הסכימה, `DatabaseSeeder` מוסיף את הפעולות הראשוניות. ה-Seeder לא דורס הגדרות של פעולות קיימות.

> הערה: הפרויקט משתמש כרגע ב-`EnsureCreated` לאתחול אוטומטי פשוט. `EnsureCreated` אינו מנגנון schema migrations. אם משנים את מבנה הישויות לאחר שכבר נוצר DB, יש לגבות ולמחוק את DB המקומי כדי ליצור אותו מחדש, או להעביר את הפרויקט למסלול EF Migrations לפני שימור נתונים חשובים.

## 2. הפעלת Backend

פתח PowerShell בתיקיית הפרויקט:

```powershell
cd .\OperationsPlatform

dotnet --list-sdks
dotnet restore .\OperationsPlatform.sln
dotnet build .\OperationsPlatform.sln
dotnet run --project .\Backend\OperationsPlatform.Api\OperationsPlatform.Api.csproj
```

השרת יעלה בכתובת:

- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger
- Health: http://localhost:5080/health

בעת Build של ה-Solution, פרויקט ה-CSharpWorker נבנה ומועתק לתיקיית `csharp-worker` ליד פלט ה-API. יש לבצע Build של ה-Solution כולו, ולא לבנות רק את פרויקט ה-API.

## 3. הפעלת Angular 12

יש להשתמש ב-Node 14.15 ומעלה בתוך קו Node 14. אם מותקן Node 22, הוא אינו תואם ל-Angular 12. ניתן לנהל גרסאות Node ב-Windows באמצעות nvm-windows.

בחלון PowerShell נוסף:

```powershell
cd .\OperationsPlatform\Frontend
npm install
npm start
```

הממשק יעלה בכתובת http://localhost:4200.

כתובת ה-API מוגדרת ב:

`Frontend/src/environments/environment.ts`

```ts
apiBaseUrl: 'http://localhost:5080/api'
```

אם שינית את כתובת השרת, עדכן את הערך הזה. בצד השרת, עדכן גם את `Frontend:AllowedOrigins` ב-`appsettings.json` אם כתובת ה-Frontend שונה.

## 4. קונפיגורציות חשובות ב-appsettings.json

| Key | שימוש | ברירת מחדל |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | חיבור ל-SQL Server | localhost / OperationsPlatformDb |
| `Frontend:AllowedOrigins` | CORS ל-Angular | http://localhost:4200 |
| `CSharpWorker:RelativeDllPath` | מיקום ה-Worker | csharp-worker/OperationsPlatform.CSharpWorker.dll |
| `CSharpWorker:ExecutionTimeoutSeconds` | Timeout לקוד C# | 8 שניות |
| `CSharpWorker:MaxSourceCodeLength` | מגבלת אורך קוד C# | 30,000 תווים |
| `RestApi:DefaultTimeoutSeconds` | Timeout ברירת מחדל לקריאות חיצוניות | 15 שניות |
| `RestApi:MaxResponseBytes` | גודל תשובת API מקסימלי | 1MB |
| `RestApi:AllowPrivateNetworkTargets` | האם לאפשר קריאות לכתובות IP פרטיות/פנימיות | true בסביבת התרגיל |
| `Execution:TimeZoneId` | קביעת תחילת החודש לספירה | Israel Standard Time |
| `Execution:MaxInputLength` | מגבלת אורך כל Input | 10,000 תווים |
| `Execution:MaxBuilderNodes` | מקסימום Nodes ב-Builder | 30 |
| `Logging:MaxBodyChars` | מספר תווים מרבי שנשמר מגוף Request/Response | 12,000 |

## 5. איך מוסיפים פעולה חדשה?

### Built-in Handler

בחר `Built-in Handler` במסך Create Operation, הגדר Code, שם, שני Inputs ו-Configuration JSON. ה-JSON חייב לכלול `handlerKey` שקיים בשרת, למשל:

```json
{
  "handlerKey": "string.concatenate",
  "separator": " | "
}
```

פעולת Built-in חדשה במובן של Configuration יכולה להשתמש בפונקציה קיימת ללא שינוי קוד. אם רוצים אלגוריתם חדש שלא קיים בספרייה, יש להוסיף Handler ב-C# ולפרוס Build חדש.

### Custom C# Operation

בחר `Custom C#`. הממשק כולל עורך קוד וכפתור Validate. הקוד חייב להגדיר מחלקה `CustomOperation` ומתודה סטטית בעלת החתימה המדויקת:

```csharp
using System.Text.Json;

public static class CustomOperation
{
    public static object? Execute(string? fieldA, string? fieldB, JsonElement configuration)
    {
        return $"{fieldA}{fieldB}";
    }
}
```

המתודה מקבלת תמיד Field A, Field B ו-Configuration נוסף. יש להחזיר ערך שניתן לסריאליזציה ל-JSON. המימוש הנוכחי סינכרוני.

### REST API Operation

בחר Implementation Type = `REST API`. דוגמת Configuration:

```json
{
  "httpMethod": "GET",
  "url": "https://example.org/lookup",
  "queryParameters": {
    "first": "{{fieldA}}",
    "second": "{{fieldB}}"
  },
  "headers": {
    "Accept": "application/json"
  },
  "timeoutSeconds": 15,
  "responseType": "Json"
}
```

תומך ב-GET/POST/PUT/DELETE וב-Headers, Query Parameters, `bodyTemplate`, `contentType`, Timeout, Text/Json response. אפשר להוסיף Authentication בקונפיגורציה:

```json
"authentication": {
  "type": "Bearer",
  "secretValue": "YOUR_TOKEN"
}
```

או `ApiKeyHeader` עם `headerName` ו-`secretValue`. בדוגמה זו הסוד נמצא ב-DB המקומי; בסביבה אמיתית יש להעביר אותו ל-Secret Store ולא לשמור אותו ב-JSON. מנגנון הלוגים מסתיר שדות בשם `secretValue`, `apiKey`, `token`, `password` ו-`authorization`.

### Operation Builder

פתח את Builder מה-Sidebar. הוסף Nodes, בחר Handler לכל Node, והגדר מאיפה כל Input מגיע:

- `$fieldA` – ערך ההתחלה הראשון.
- `$fieldB` – ערך ההתחלה השני.
- `stepId.output` – פלט Node קודם.
- `literal:some text` – ערך קבוע.

Nodes מופעלים לפי הסדר מלמעלה למטה. בגרסה זו נתמכים רצפים וחיבורים לערכים קודמים בלבד; אין לולאות, הסתעפויות, או הרצה מקבילית. ה-Builder מפעיל Built-in handlers בלבד.

## 6. API Endpoints

| Method | Endpoint | תיאור |
|---|---|---|
| GET | `/api/operations` | רשימת פעולות פעילות |
| GET | `/api/operations?includeInactive=true` | רשימה כולל מושבתות |
| GET | `/api/operations/{id}` | פרטי פעולה |
| POST | `/api/operations` | יצירת פעולה |
| PUT | `/api/operations/{id}` | עדכון פעולה |
| DELETE | `/api/operations/{id}` | Soft delete |
| POST | `/api/operations/{id}/execute` | הפעלת פעולה |
| POST | `/api/operations/validate-csharp` | בדיקת קומפילציה לקוד C# |
| GET | `/api/history` | היסטוריית הרצות עם pagination |
| GET | `/api/logs` | לוגים עם pagination |
| GET | `/api/dashboard` | מדדי Dashboard |

דוגמת Execute:

```http
POST http://localhost:5080/api/operations/1/execute
Content-Type: application/json

{
  "fieldA": "Hello",
  "fieldB": "World"
}
```

כל פעולה נרשמת ב-`OperationExecutions`. התשובה כוללת את התוצאה, זמן הביצוע, שלוש ההרצות האחרונות של אותה פעולה ואת מספר ההרצות מתחילת החודש המקומי.

## 7. Weather Forecast

הפעולה המובנית `Weather Forecast` משתמשת ב-Open-Meteo:

`https://api.open-meteo.com/v1/forecast`

היא מקבלת Latitude ו-Longitude ומחזירה נתוני מזג אוויר נוכחיים בפורמט JSON. השירות הציבורי אינו דורש API Key לשימוש החינמי הלא-מסחרי, ויש לתת קרדיט ל-Open-Meteo בהתאם לרישיון CC BY 4.0: https://open-meteo.com/.

## 8. אבטחה – קריאה חשובה

המערכת מיועדת במפורש לסביבת תרגיל מקומית/ארגונית סגורה, ללא Login. אין לחשוף אותה לאינטרנט או לרשת לא מהימנה.

- Custom C#: ה-Worker הוא תהליך נפרד עם Timeout, אבל **אינו Sandbox**. קוד C# עלול לגשת לקבצים, לרשת ולמשאבי Windows תחת המשתמש שמריץ את השרת. יש לאפשר רק קוד ממקור מהימן. לבידוד אמיתי יש להריץ Worker תחת משתמש Windows נפרד/Container עם הרשאות ורשת מוגבלות.
- REST URL: הגדרת כתובות חופשית עלולה לאפשר SSRF. בפרויקט התרגיל `AllowPrivateNetworkTargets=true` כדי לאפשר כתובות Intranet כפי שביקשת. אל תפעיל הגדרה זו בשרת חשוף או עם משתמשים לא מהימנים. אם נדרשות רק כתובות ציבוריות, שנה ל-`false`.
- אין Authentication/Authorization. כל גורם שמגיע ל-API יכול להוסיף ולערוך פעולות, כולל קוד C# וכתובות REST.
- סודות Authentication נשמרים כטקסט ב-JSON של SQL בפרויקט הדגמה זה, אך מוסתרים בתשובות GET של ה-API. בעת עריכה, אם לא מזינים סוד חדש ומשאירים את אותו סוג Authentication, הערך הקיים נשמר; כדי להסיר Authentication יש לבחור None. אין להשתמש בסודות אמיתיים.
- ה-Request/Response logs מוגבלים באורך ומבצעים Masking לשדות סודיים נפוצים, אך אין להזין מידע אישי/רגיש שאינו נחוץ.

## 9. פתרון תקלות

**השרת לא מתחבר ל-SQL:**
1. בדוק את שם ה-Server/Instance ב-SSMS.
2. ודא ששירות SQL Server פועל.
3. ודא של-Windows account שמריץ את API יש הרשאה ליצור Database.
4. עדכן `DefaultConnection`.

**C# Worker לא נמצא:**
1. פתח את ה-Solution כולו.
2. הרץ `dotnet clean` ואז `dotnet build OperationsPlatform.sln`.
3. ודא שבפלט ה-API קיימת תיקייה `csharp-worker` עם `OperationsPlatform.CSharpWorker.dll` ו-runtimeconfig/deps.
4. ודא ש-`dotnet` זמין ב-PATH.

**Angular לא מתחבר ל-API:**
1. פתח `http://localhost:5080/health`.
2. בדוק את `apiBaseUrl` ב-`environment.ts`.
3. בדוק את `Frontend:AllowedOrigins` ב-appsettings.

**Weather נכשל:**
1. ודא שיש חיבור אינטרנט מהמחשב שבו ה-Backend פועל.
2. בדוק שכתובת Open-Meteo נגישה.
3. ודא שהוזנו Latitude ו-Longitude בטווחים תקינים.

## 10. מבנה תיקיות

```text
OperationsPlatform/
├── OperationsPlatform.sln
├── Backend/
│   ├── OperationsPlatform.Api/
│   │   ├── Controllers/
│   │   ├── Data/
│   │   ├── Entities/
│   │   ├── Middleware/
│   │   ├── Models/
│   │   ├── Services/
│   │   ├── appsettings.json
│   │   └── Program.cs
│   └── OperationsPlatform.CSharpWorker/
└── Frontend/
    ├── src/app/
    │   ├── builder/
    │   ├── dashboard/
    │   ├── execute/
    │   ├── history/
    │   ├── logs/
    │   ├── operation-editor/
    │   ├── operations/
    │   ├── services/
    │   └── app.component.*
    └── package.json
```
