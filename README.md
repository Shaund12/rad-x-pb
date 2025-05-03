<!--
  Rad‑X Price Bot  •  README
  ------------------------------------------------------------
  A native WPF desktop app that turns your Discord bot into a
  live on‑chain dashboard for any EVM‑compatible network.
-->

# Rad‑X Price Bot Desktop 🏷️

![GitHub release](https://img.shields.io/github/v/release/your‑repo/radxpricebot?logo=github)
![CI](https://img.shields.io/github/actions/workflow/status/your‑repo/radxpricebot/dotnet.yml?label=build&logo=githubactions)
![License](https://img.shields.io/github/license/your‑repo/radxpricebot)

A **.NET 9 / WPF** application that lets you run **one or many Discord price‑tracking bots** from a single desktop dashboard.  
No web‑hosting or command‑line kung‑fu required—just download, extract, and click **`radxpricebot.exe`**.

---

## ✨ Features

| Category | Highlights |
|----------|------------|
| **Real‑Time Metrics** | Price, USD price, reverse rate, liquidity, 24 h volume, market‑cap & more—all fetched on schedule from any Uniswap‑V2‑style router. |
| **Multi‑Bot Manager** | Create unlimited bot configs, each with its own token path, RPC, guild, presence template & update interval. Start/stop individually or all at once. |
| **Rich Discord Embeds** | Periodic embeds with customizable color, optional chart snapshot, token stats and liquidity info. |
| **Swap Monitoring** | Detect buy/sell swaps in the selected pair and push detailed alerts to a Discord channel. |
| **Dashboard & Logs** | Live grid of bot status (running, last update, price, liquidity) plus a multi‑tabbed log console. |
| **UI Scaling** | Built‑in DPI combo (75 %‑200 %) powered by `ScaleTransform`; minimum window size adapts to your scaling. |
| **Any EVM Network** | Ethereum, Polygon, BNB Smart Chain, Arbitrum, Optimism, Avalanche, Base, or a private chain—just swap the RPC and router. |
| **SQLite Persistence** | Settings, bot configs and historical metrics are stored locally (30‑day auto‑cleanup). |
| **AES‑256 Secure Storage** | Tokens & sensitive settings are encrypted at rest. |
| **Modern UX** | Neon/glassmorphism UI, smooth scroll‑reveal, animated gradient buttons and tilt cards (see `styles/`). |

---

## 📦 Quick Start (Windows x64)

```text
1. Download  :  radxpb.zip  (latest release)
2. Extract   :  Right‑click → “Extract All…”
3. Run       :  double‑click  radxpricebot.exe
````

That’s it—Rad‑X Price Bot will open its dashboard and prompt for your first Discord bot token.

> **Heads‑up:** The app targets **.NET 9 Desktop Runtime**.
> If the EXE refuses to start, install the runtime from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0/runtime).

---

## 🛠️ Building From Source

### Requirements

* Windows 10/11 x64
* **.NET 9 SDK** (preview)
  `winget install Microsoft.DotNet.SDK.9`
* VS 2022 v17.10 Preview **or** `dotnet cli`

### Clone & Build

```bash
git clone https://github.com/your‑repo/radxpricebot.git
cd radxpricebot

# CLI
dotnet restore
dotnet build -c Release          # EXE drops in  bin\Release\net9.0-windows\

# or open RadXPriceBot.sln in Visual Studio and hit ▶
```

Continuous integration (see `.github/workflows/dotnet.yml`) runs the same commands on every push.

---

## 🤖 Discord Bot Setup

1. **Create a bot** in the [Discord Developer Portal](https://discord.com/developers/applications).
   *Bot → “Add Bot” → copy **Token***
   Enable *Presence* & *Server Members* intents.
2. **Invite** it to your server
   OAuth2 → URL Generator → scopes `bot applications.commands`
   Select permissions: **Send Messages, Embed Links, Change Nickname**.
3. **Configure in Rad‑X**
   *Bot Manager → New Bot*
   • Paste Token   • Guild ID   • RPC URL   • Router Address
   Load a pair, hit **“Start Selected Bot”**.

---

## 🖥️ Main Window Tour

| UI Area              | Purpose                                                                                            |
| -------------------- | -------------------------------------------------------------------------------------------------- |
| **LP Pairs**         | Pull pairs from factory; select one to preview metrics.                                            |
| **Metrics Panel**    | Price, volume, reserves—updates whenever you switch pairs.                                         |
| **Bot Manager Tabs** | Set status type (Price / Reserves / Market‑Cap / Custom), embed & swap settings.                   |
| **Dashboard**        | DataGrid listing each running bot with live status, start/stop buttons and “Details” pop‑ups.      |
| **Logs**             | Global log + per‑bot log (multi‑tab) with timestamped entries from `AppendLog`.                    |
| **Menu**             | Settings (global), Documentation / FAQ / About (Markdown pop‑ups), Exit (auto‑saves + stops bots). |
| **DPI Scaling**      | Combo box top‑right; `ScaleTransform` resizes everything and persists to `AppSettings.DpiScaling`. |

---

## 🪄 Customizing Status Text

*Price* example

```
{symbol0}/{symbol1}: ${price}
```

*Custom* example

```
LP ${liquidity} • MC ${marketcap}
```

| Placeholder   | Description                       |
| ------------- | --------------------------------- |
| `{price}`     | Current token price (quote token) |
| `{symbol0}`   | Base token symbol                 |
| `{symbol1}`   | Quote token symbol                |
| `{liquidity}` | Pool liquidity (USD)              |
| `{marketcap}` | Token market‑cap (USD)            |

---

## 🔔 Swap Monitoring 101

1. **Enable** “Monitor Swap Transactions” in the Swap tab.
2. **Channel ID**   → where alerts go.
3. **Interval (ms)**   → how often to poll pair contract logs.

Rad‑X emits buy/sell embeds showing:

* Action (Buy/Sell) & amount
* Price impact %
* Updated reserves & price

---

## 📸 Screenshots

> Replace the image paths with real captures once you commit them.

| Dashboard                      | Bot Manager                      | LP Insights                       |
| ------------------------------ | -------------------------------- | --------------------------------- |
| ![](screenshots/dashboard.png) | ![](screenshots/bot‑manager.png) | ![](screenshots/pair‑details.png) |

---

## 🧩 Architecture

```text
┌─ UI (WPF) ─┐
│ MainWindow │←───┐  MVVM commands bind to →
└────────────┘    │
     ▲            ▼
┌────────────┐  MainViewModel
│  Services  │     ▲
│  • Price   │     │
│  • Discord │     │
│  • Storage │     │
└────────────┘  Entity Models (BotConfig, PairInfo, etc.)
```

* **Discord.NET 3.x** handles gateway + REST.
* **Nethereum** queries router/factory/pair contracts.
* **EF Core (SQLite)** stores settings/history with 30‑day cleanup.
* **Multi‑threading**: UI thread ↔ background tasks via async/await.

---

## 📝 Roadmap

* [ ] **TradingView mini‑chart** inside embeds
* [ ] **gRPC** local API for headless usage
* [ ] **Auto‑updater** (check GitHub releases)
* [ ] **Cross‑platform UI** via Avalonia XPF (stretch goal)

---

## 🤝 Contributing

PRs are welcome!  Please:

1. Create a feature branch
2. Run `dotnet format`
3. Add/extend unit tests if you touch core logic
4. Open a pull request describing your change

---

## 🗒️ License

Rad‑X Price Bot is released under the **MIT License** – see [`LICENSE`](LICENSE).

---

## ⭐ Show your support

If this project saves you time, please **star the repo** ⭐.
It helps others discover the tool and keeps the lights green!

---

> *Built with ❤️ by the Rad‑X Development Team – 2025.*
