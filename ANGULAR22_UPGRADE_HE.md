# עדכון Frontend ל-Angular 22

החבילה הזו מעדכנת את ה-Frontend מ-Angular 12 ל-Angular 22.2.7. ה-Backend וה-API לא שונו בעדכון הזה.

## מה השתנה?

- Angular framework, Angular CLI, Angular Material ו-CDK עודכנו ל-22.2.7.
- TypeScript עודכן ל-6.0.x ו-RxJS ל-7.8.x.
- ה-Webpack browser builder הוחלף ב-`@angular/build:application` וב-`@angular/build:dev-server`.
- קונפיגורציית TypeScript הותאמה ל-`moduleResolution: bundler`.
- Angular Material theming הועבר ל-Material 3 Sass API.
- נשמרו מסכי Dashboard, Operations, Editor, Execute, Builder, History ו-Logs, וכן Dark/Light Theme וחוזי ה-API הקיימים.
- `start-frontend.ps1` בודק שגרסת Node מתאימה וגם מזהה אם עדיין מותקנת גרסת Angular ישנה ב-`node_modules`.

## דרישת Node.js

Angular 22 דורש אחת מהגרסאות הבאות:

- Node.js `22.22.3` ומעלה בתוך קו 22.
- Node.js `24.15.0` ומעלה בתוך קו 24.
- Node.js 26.x.

Node.js `24.13.1` שמותקן כרגע אצלך אינו עומד בדרישת Angular 22. עדכן ל-Node.js 24.15.0 או לגרסה נתמכת מאוחרת יותר. אפשר גם להשתמש ב-22.22.3 ומעלה.

טבלת תאימות רשמית: https://angular.dev/reference/versions

## התקנת העדכון בפרויקט קיים

1. עצור את שרת Angular אם הוא פועל.
2. גבה או שנה את שם תיקיית `Frontend` הקיימת.
3. העתק את תיקיית `Frontend` מתוך חבילת העדכון למיקום המקורי בפרויקט.
4. העתק את `scripts/start-frontend.ps1` מתוך חבילת העדכון מעל הקובץ הקיים בפרויקט.
5. עדכן את Node.js לגרסה נתמכת לפי הסעיף הקודם.
6. פתח PowerShell חדש ובדוק:

   ```powershell
   node --version
   npm --version
   ```

7. מחק תיקיית dependencies ישנה אם קיימת:

   ```powershell
   Remove-Item -Recurse -Force .\Frontend\node_modules -ErrorAction SilentlyContinue
   Remove-Item -Force .\Frontend\package-lock.json -ErrorAction SilentlyContinue
   ```

8. הפעל מתוך שורש הפרויקט:

   ```powershell
   .\scripts\start-frontend.ps1
   ```

   לחלופין, מתוך `Frontend`:

   ```powershell
   npm install
   npm start
   ```

## Build ל-Production

מתוך `Frontend`:

```powershell
npm run build
```

הקבצים יופקו בתיקיית `Frontend/dist/operations-platform-client`.

## קונפיגורציית API

כתובת ה-API נשארה בקובצי Angular environment:

- `src/environments/environment.ts` לפיתוח מקומי.
- `src/environments/environment.prod.ts` ל-build של Production.

ברירת המחדל היא `http://localhost:5080/api`. אין צורך לשנות Backend בעקבות שדרוג Angular, כל עוד כתובת השרת נשארת זהה.
