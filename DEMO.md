# Demo guide - Majd's Platform

A 12-minute walkthrough for a supervisor. Local development only: these accounts and passwords are for the demo database on this computer.

## Before you start (2 minutes)

1. Close the old Razor site if it is open (it is not needed for this demo, but it is fine if it runs).
2. Double-click **`start-demo.bat`** in this folder. Two black windows open (API and web app). When both are ready, the browser opens on the login page.
3. Use a normal window for the admin and keep a **private (incognito) window** ready for the limited user in step 4.

## Accounts

| Who | Email | Password | What they are |
|---|---|---|---|
| Demo admin | `demo.admin@example.com` | `Demo1234!` | Admin: sees everything |
| Sara Ahmad | `sara.ahmad@example.com` | `Demo1234!` | Viewer role: can only open Users and Audit Log |
| Omar Khalil | `omar.khalil@example.com` | `Demo1234!` | Plain User: no admin access |
| Your own account | your Gmail | your password | Has two-factor on, use it for the 2FA and Google steps |

## The walkthrough

**1. Sign in and the dashboard (1 min)**
Sign in as the demo admin. Point out the **System health** card (database and file storage both Healthy) and the menu on the left.
> Say: "This is a reusable platform. Everything you see is generic, none of it belongs to one business."

**2. Users and Roles (2 min)**
Open **Roles**: there are Admin, User and Viewer. Click the key icon on **Viewer**: only `Users.View` and `Audit.View` are ticked. Open **Users** to see the people, their roles and status.
> Say: "Access is granted by roles. A new account starts with nothing."

**3. Create a user live (1 min)**
Users, New user, give an email, a password and the Viewer role. Note the confirmation message.

**4. The limited user (2 min) - the most important step**
In the private window sign in as **Sara**. The menu shows only Dashboard, Files, Users and Audit Log. Users has no Edit or Delete buttons.
Then sign in as **Omar** to show an even shorter menu.
> Say: "This is enforced on the server too, not just hidden. Typing a hidden page address sends them back."

**5. Settings and Feature flags (1.5 min)**
As admin: **Settings**, change the application name, save, and see the header change. Then **Features**: turn **File storage** off, watch Files leave the menu, turn it back on.
> Say: "An administrator can change behaviour without a new release."

**6. Audit log (1 min)**
Open **Audit Log**: every action so far is listed with the person and time. Click **Export CSV**.
> Say: "Every sensitive action is recorded."

**7. Files and Notifications (1 min)**
Open **Files**: one sample document. Upload another, download it. Click the **bell**: there is a welcome notification.

**8. Search and Language (1 min)**
Type `sara` in the header search and click the result: the Users list opens already filtered, with a clear chip. Then switch language to **العربية**: text translates and the layout flips right to left.

**9. Background jobs (30 sec)**
Open **Background Jobs**, click the play button on a job, and see it appear under Recent runs.

**10. Security: two-factor and Google (1.5 min)**
Sign in with your own account: after the password it asks for the code from your phone. Then sign out and click **Continue with Google**: an existing account signs straight in, a new Google account creates a profile.

## If something goes wrong

- **Login page has no Google button or "Create account" link**: the API is not running. Look at the "Majd API" window for an error, or run `start-demo.bat` again.
- **"Address already in use"**: an earlier copy is still running. Close the old black windows, or restart the computer.
- **Google shows redirect_uri_mismatch**: add `http://localhost:5156/signin-google` to the OAuth client in Google Cloud Console.
- **Signed out unexpectedly**: sessions last 60 minutes by default (Settings, Session timeout).
- **Page shows an old version**: press `Ctrl+F5`.

## After the demo

- The sample users and Viewer role can be removed from the **Users** and **Roles** pages.
- To remove the demo admin, sign in as your own account and delete `demo.admin@example.com` from **Users**.
