# Publishing Roam for free: a step-by-step guide

This guide takes you from "the code is on my computer" to "anyone can open **https://roam.your-domain.com**".
No previous server experience is needed. Copy each command exactly as shown.

**What it costs:** nothing.
- A free server: Oracle Cloud's "Always Free" tier, or your own Windows PC.
- Free HTTPS and a free tunnel: Cloudflare, with the domain you already have.
- A free video relay: Cloudflare Realtime TURN. The first 1,000 GB each month are free.
- Free email for password resets: a Gmail app password.

**Time needed:** about 1–2 hours the first time.

**How it fits together**

```
Visitors ──https──> Cloudflare ──tunnel──> your server:  web (app + proxy) ─> 2 × API ─> PostgreSQL
                                                                              └──────> Redis
```

Your server never opens a port to the internet. The `cloudflared` container dials out to Cloudflare, and
Cloudflare provides the HTTPS certificate.

---

## Contents

1. [Put your latest code on GitHub](#1-put-your-latest-code-on-github)
2. [Get a free server](#2-get-a-free-server) (Option A: Oracle Cloud, recommended. Option B: your own PC)
3. [Install Docker on the server](#3-install-docker-on-the-server)
4. [Download Roam onto the server](#4-download-roam-onto-the-server)
5. [Create the settings file](#5-create-the-settings-file)
6. [Connect your domain with a Cloudflare Tunnel](#6-connect-your-domain-with-a-cloudflare-tunnel)
7. [Start Roam](#7-start-roam)
8. [Turn on the video relay (recommended)](#8-turn-on-the-video-relay-recommended)
9. [Turn on email for password resets (recommended)](#9-turn-on-email-for-password-resets-recommended)
10. [Automatic daily backups](#10-automatic-daily-backups)
11. [Final checks before you tell people](#11-final-checks-before-you-tell-people)
12. [Everyday tasks: updating, logs, restarting](#12-everyday-tasks)
13. [Troubleshooting](#13-troubleshooting)
14. [Good to know](#14-good-to-know)

---

## 1. Put your latest code on GitHub

The server downloads Roam from GitHub, so GitHub must have your latest version.

On your Windows PC, open **PowerShell** in the project folder
(`C:\Users\<you>\Desktop\Code\Projects\RoamApplication\Roam`) and run:

```powershell
git add -A
git commit -m "Prepare Roam for production"
git push
```

> If your GitHub repository is **private**, the server will need a key to download it. Create one now:
> 1. Go to GitHub → your profile picture → **Settings** → **Developer settings** → **Personal access tokens** →
>    **Fine-grained tokens** → **Generate new token**.
> 2. Name it `roam-server` and set the expiration you prefer.
> 3. Under **Repository access**, choose **Only select repositories** and pick **Roam**.
> 4. Under **Permissions → Repository permissions**, set **Contents** to **Read-only**.
> 5. Click **Generate token** and copy it into Notepad. You will paste it in step 4. GitHub shows it only once.

---

## 2. Get a free server

Pick **one** option.

### Option A (recommended): Oracle Cloud "Always Free"

This gives you a real server that runs 24/7 for free, with 4 CPU cores and 24 GB of memory to share.

1. Go to <https://www.oracle.com/cloud/free/> and click **Start for free**.
2. Fill in the form.
   - **Home region:** pick one close to your users. **It cannot be changed later**, and you can only create
     free servers there.
   - Oracle asks for a credit or debit card **to verify your identity**. Free resources are not charged, and
     your account stays "Free Tier" unless you choose to upgrade.
3. When you are in the Oracle Cloud console, click the **☰ menu** → **Compute** → **Instances** → **Create instance**.
4. Fill in:
   - **Name:** `roam`
   - **Image and shape** → **Edit**:
     - **Image:** click **Change image** → **Ubuntu** → pick **Canonical Ubuntu 24.04** (not the "Minimal" one) → **Select image**.
     - **Shape:** click **Change shape** → **Ampere** → **VM.Standard.A1.Flex**. Set **OCPUs = 2** and **Memory = 12 GB**.
       This stays inside the free limit and leaves room for a second server later. → **Select shape**.
   - **Networking:** leave the defaults. Make sure **Assign a public IPv4 address** is selected.
   - **Add SSH keys:** choose **Generate a key pair for me** and click **Save private key**. Keep that file safe:
     it is the only way into your server. Move it to `C:\Users\<you>\.ssh\roam.key`.
5. Click **Create**. After 1–2 minutes the status turns **Running**. Copy the **Public IP address**, for example `130.61.10.20`.

> **"Out of capacity for shape VM.Standard.A1.Flex"?** Free ARM servers are popular. Try again later, or pick
> a different **Availability domain** (AD-1, AD-2, AD-3) in the placement section.

**Connect to the server.** On Windows, open **PowerShell** and run the commands below. Replace the IP with yours.

The first command makes the key file private, which `ssh` requires:

```powershell
icacls "$env:USERPROFILE\.ssh\roam.key" /inheritance:r /grant:r "$($env:USERNAME):(R)"
```

The second command connects:

```powershell
ssh -i "$env:USERPROFILE\.ssh\roam.key" ubuntu@130.61.10.20
```

Type `yes` the first time. Your prompt now looks like `ubuntu@roam:~$`, which means **you are on the server**.
Every Linux command from here on goes into this window.

> **Oracle may stop idle free servers.** Oracle can reclaim "Always Free" servers that stay almost completely
> idle for 7 days. A live Roam site normally has some activity. If your server is reclaimed, you can create it
> again and restore a backup (step 10).
>
> To rule this out completely, upgrade the account to **Pay As You Go**: ☰ → **Billing** → **Upgrade and
> manage payment**. Always Free resources stay free after the upgrade. If you do this, also create a **budget
> alert** of 1 (☰ → **Billing** → **Budgets**) so you get an email if anything ever costs money.

### Option B: your own Windows PC

This is free and needs no card, but **the site is only online while your PC is on and awake**.

1. Install **Git for Windows** from <https://git-scm.com/download/win> (accept the defaults).
2. Install **Docker Desktop** from <https://www.docker.com/products/docker-desktop/>, restart your PC, open
   Docker Desktop, and in **Settings → General** turn on **Start Docker Desktop when you sign in**.
3. In Windows **Settings → System → Power**, set **Sleep** to **Never** while plugged in.
4. Open **Git Bash** from the Start menu. Use Git Bash for every Linux command in this guide, then skip
   step 3 and go straight to step 4.

---

## 3. Install Docker on the server

*(Option A only. Run these on the server.)*

This command installs Docker:

```bash
curl -fsSL https://get.docker.com | sudo sh
```

This one lets your user run Docker without `sudo`:

```bash
sudo usermod -aG docker $USER
```

Now **log out** with `exit`, then connect again with the same `ssh` command as before. Check that Docker works:

```bash
docker run --rm hello-world
```

You should see "Hello from Docker!".

---

## 4. Download Roam onto the server

```bash
git clone https://github.com/Beerkanwar/Roam.git
```

If the repository is private, Git asks for a **Username** (your GitHub username) and a **Password**.
For the password, paste the **token** from step 1. When you paste into a terminal, nothing is shown; that is normal.

Next, let the server remember the token, so updates don't ask again:

```bash
cd Roam && git config credential.helper store && git pull && cd ..
```

---

## 5. Create the settings file

```bash
cd ~/Roam/deploy
bash setup.sh
```

On Option B (your PC), first `cd` to the project's `deploy` folder in Git Bash instead of `~/Roam/deploy`.

The script asks five questions:

| Question | What to type |
|---|---|
| Web address | The address people will use, for example `roam.your-domain.com` (no `https://`) |
| Your name or organisation | Shown on the Privacy and Terms pages |
| Contact email | Shown on the Privacy page, and used to identify Roam to the free map search service |
| Administrator email | Your email. Roam creates an admin account with it |
| Cloudflare Tunnel token | Just press **Enter**; you will add it in step 6 |

At the end, it prints the **administrator password**. **Write it down.**

The script created `deploy/.env`, which holds every password and encryption key. **Keep a copy somewhere safe**,
for example a password manager:

```bash
cat .env
```

Without this file, a restored backup cannot be opened and everyone would have to sign in again.

---

## 6. Connect your domain with a Cloudflare Tunnel

1. Log in to <https://dash.cloudflare.com>.
2. In the left menu, open **Zero Trust**. If asked, choose a team name (anything) and the **Free** plan.
3. Go to **Networks → Tunnels** → **Create a tunnel** → choose **Cloudflared** → name it `roam` → **Save tunnel**.
4. On the "Install and run connectors" screen, pick **Docker**. You will see a command like
   `docker run cloudflare/cloudflared:latest tunnel --no-autoupdate run --token eyJhIjoi...`.
   Copy **only the long text after `--token `**. You do **not** need to run that command.
5. Click **Next**. Add a **public hostname** (newer dashboards call it a *published application route*):
   - **Subdomain:** `roam` (or whatever you entered in step 5)
   - **Domain:** pick your domain
   - **Service type:** `HTTP`
   - **URL:** `web:8080`

   Click **Save**.
6. Back on the server, open the settings file:

   ```bash
   nano .env
   ```

   Use the arrow keys to edit these two lines:

   ```
   CLOUDFLARE_TUNNEL_TOKEN=eyJhIjoi...the long token...
   COMPOSE_PROFILES=tunnel
   ```

   Save with **Ctrl+O**, then **Enter**, and exit with **Ctrl+X**.
7. Still in Cloudflare: open your domain → **SSL/TLS** → **Edge Certificates** → turn on **Always Use HTTPS**.

---

## 7. Start Roam

```bash
docker compose up -d --build
```

The first build takes **10–20 minutes**. Later starts take seconds. When it finishes, check the status:

```bash
docker compose ps
```

Every service should say `running` or `healthy`. If one doesn't, see [Troubleshooting](#13-troubleshooting).

Open **https://roam.your-domain.com** in your browser. Sign in with the administrator email and password
from step 5. You should see a **🛡️ Admin** tab, where volunteer applications and safety reports appear.

Once email is set up (step 9), **change the admin password**: sign out, click **Forgot your password?** and
follow the link in the email. Then delete the value after `BOOTSTRAP_ADMIN_PASSWORD=` in `.env`.

---

## 8. Turn on the video relay (recommended)

About 1 in 5 connections, mostly on mobile networks and office Wi-Fi, cannot connect video directly. A TURN
relay fixes that.

1. In the Cloudflare dashboard, open **Realtime** (also called **Calls**) → **TURN Server** → **Create**.
   Name it `roam`.
2. Copy the **Turn Token ID** and the **API Token**. The API token is shown only once.
3. On the server, run `nano .env` and set:

   ```
   TURN_PROVIDER=cloudflare
   CLOUDFLARE_TURN_KEY_ID=...the Turn Token ID...
   CLOUDFLARE_TURN_API_TOKEN=...the API token...
   ```

4. Apply the change:

   ```bash
   docker compose up -d
   ```

> Cloudflare's pricing page says the first 1,000 GB of relayed traffic each month are free (shared with their
> other Realtime services). A small site stays far below that. You can see your usage in the Realtime section.

---

## 9. Turn on email for password resets (recommended)

Without email, people who forget their password cannot get back in. The simplest free option is a Gmail
account. A new Gmail account just for Roam is a good idea.

1. Sign in to that Google account and go to <https://myaccount.google.com/security>.
2. Turn on **2-Step Verification** if it's off.
3. Search for **App passwords** in the account search box at the top. Create one named `Roam`, and copy the
   16-letter password **without spaces**.
4. On the server, run `nano .env` and set:

   ```
   SMTP_HOST=smtp.gmail.com
   SMTP_PORT=587
   SMTP_USERNAME=your.roam.account@gmail.com
   SMTP_PASSWORD=abcdabcdabcdabcd
   SMTP_FROM_EMAIL=your.roam.account@gmail.com
   SMTP_FROM_NAME=Roam
   ```

5. Apply the change:

   ```bash
   docker compose up -d
   ```

Test it: sign out, click **Forgot your password?**, and enter the admin email. An email with a code should arrive
within a minute (check spam).

> Password reset only works for confirmed email addresses. New users get a confirmation email when they
> register. They can resend it from their profile, or from the "Forgot your password?" screen.
> Gmail allows roughly 500 emails a day, which is plenty for a new site.

---

## 10. Automatic daily backups

This backs up the database every night at 03:00 and keeps the last 14 days.

```bash
chmod +x ~/Roam/deploy/*.sh
crontab -e
```

If asked which editor to use, press **1** (nano). Go to the very bottom and add this line:

```
0 3 * * * /home/ubuntu/Roam/deploy/backup.sh >> /home/ubuntu/roam-backup.log 2>&1
```

Save with **Ctrl+O**, then **Enter**, and exit with **Ctrl+X**. Run one backup now to check that it works:

```bash
~/Roam/deploy/backup.sh
```

Backups stay **on the same server**, so copy one to your PC now and then. On your **PC**, in PowerShell:

```powershell
scp -i "$env:USERPROFILE\.ssh\roam.key" "ubuntu@130.61.10.20:~/Roam/deploy/backups/*" "$env:USERPROFILE\Documents\roam-backups\"
```

To restore a backup (this replaces all current data):

```bash
cd ~/Roam/deploy && ./restore.sh backups/roam-YYYYMMDD-HHMMSS.sql.gz
```

On Option B, use Windows **Task Scheduler** to run `"C:\Program Files\Git\bin\bash.exe" <path>\deploy\backup.sh`
every day.

---

## 11. Final checks before you tell people

Use **two different devices**, for example your phone on mobile data and a laptop, with two accounts:

- [ ] Register a new account. The Terms and Privacy links open, and the 18+ checkbox is required.
- [ ] The confirmation email arrives and its link says the email is confirmed.
- [ ] Finish the profile (18+ check), then apply to volunteer on one account.
- [ ] As admin, approve that volunteer in the **🛡️ Admin** tab.
- [ ] Request a tour on account 1. The volunteer sees it, accepts it and starts it.
- [ ] Both join. **Allow camera and microphone**, press **Start Call**, and check that video and sound work both ways.
- [ ] Chat works both ways.
- [ ] End the tour. Both get the feedback screen.
- [ ] Send a test report from **Safety & Report**. It appears in the Admin tab.
- [ ] Profile → **Download my data** works. Profile → **Delete my account** works on a test account.
- [ ] Open `https://roam.your-domain.com/privacy.html`. Read the Privacy Policy and Terms, make sure they
  describe how *you* run Roam, and edit them if needed. They are in `src/Roam.Web/public/`. Change them,
  commit, then update the server (step 12).

---

## 12. Everyday tasks

All of these commands run on the server, inside `~/Roam/deploy`.

| Task | Command |
|---|---|
| Update to the newest code (backs up first) | `./update.sh` |
| See if everything is running | `docker compose ps` |
| Watch the API logs (Ctrl+C to stop) | `docker compose logs -f api` |
| Restart everything | `docker compose restart` |
| Stop the site | `docker compose stop` |
| Start it again | `docker compose up -d` |
| Change a setting | `nano .env`, then `docker compose up -d` |

Updating is: make changes on your PC → `git push` → on the server, `./update.sh`.

---

## 13. Troubleshooting

**The site shows a Cloudflare error page (e.g. 1033 or 502).**
- Is the tunnel container running? Run `docker compose ps`. If `cloudflared` is missing, check that `.env` has
  `COMPOSE_PROFILES=tunnel` and the token, then run `docker compose up -d`.
- Check its log with `docker compose logs cloudflared`. "Unauthorized" means the token is wrong.
- In Cloudflare, the public hostname's URL must be exactly `web:8080` with type `HTTP`.

**`docker compose up` says "Run ./setup.sh first".**
There is no `.env` in the `deploy` folder. Run `bash setup.sh`.

**The API keeps restarting.** Look at the log with `docker compose logs api | tail -50`.
- "DATA_PROTECTION_CERT_BASE64 ..." means `.env` is damaged or was edited by mistake. Restore your saved copy.
- Database connection errors usually fix themselves once `db` is healthy. Wait a minute.

**Video connects for some people but not others.** Turn on the video relay (step 8).

**Place search returns nothing.** The free OpenStreetMap search allows about one request per second for the
whole site, and results are cached. Wait a few seconds and try again.

**Forgot the admin password.** Use "Forgot your password?" (needs step 9). If email isn't set up, add another
admin by setting `BOOTSTRAP_ADMIN_EMAIL` and `BOOTSTRAP_ADMIN_PASSWORD` in `.env` to a **new** email, then run
`docker compose up -d`.

**The server ran out of disk.** Check with `df -h`. Then remove old Docker images with
`docker image prune -a -f`.

---

## 14. Good to know

- **Phones:** Roam is a website, so it works in the phone's browser. People can use **Share → Add to Home
  Screen** to get an app-like icon. The separate .NET MAUI app (`src/Roam.App`) is not needed to launch, and
  app stores charge fees: Google Play has a one-time $25 fee, and Apple charges $99 per year.
- **Age checks** are based on the date of birth people enter. Approving volunteers yourself in the Admin tab is
  your main safety control, so only approve people you trust.
- **Legal:** the included Privacy Policy and Terms are a starting point, not legal advice. If you have users in
  the EU/UK, you are responsible for GDPR. Roam already includes data download and account deletion.
- **Scaling up:** set `API_REPLICAS=3` in `.env`, then run `docker compose up -d`. All API copies share
  sign-ins, rate limits, live tours and chat. Nothing else needs to change.
- **Security updates:** Ubuntu installs security updates automatically. Run `./update.sh` now and then to get
  updated Roam builds.
