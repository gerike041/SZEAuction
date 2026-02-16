# 🏗 SZEAuction – Console Auction System (.NET + PostgreSQL)

Konzolos aukció/licit rendszer .NET 8 és PostgreSQL alapokon.

---

# 👨‍💻 Development Workflow (Visual Studio)

Ez a rész bemutatja, hogyan lehet a projektet Visual Studio segítségével lehúzni, fejleszteni rajta, majd Pull Request segítségével visszailleszteni a főágba.

---

## 1️⃣ Repository klónozása Visual Studio-ból

1. Nyisd meg a **Visual Studio-t**
2. Válaszd: **Clone a repository**
3. Add meg a GitHub repository URL-t
4. Válaszd ki a helyi mentési mappát
5. Kattints: **Clone**

---

## 2️⃣ Projekt megnyitása

A klónozás után:

- Nyisd meg a `SZEAuction.sln` fájlt  
vagy  
- Visual Studio → **Open a project or solution** → válaszd ki a `.sln` fájlt

---

## 3️⃣ Saját branch használata (kötelező)

Ne közvetlenül a `main` ágon dolgozz.

Visual Studio-ban:

1. Nyisd meg a **Git Changes** panelt  
2. Kattints az aktuális branch nevére  
3. Válaszd: **New Branch**
4. Add meg az új branch nevét (pl. `feature/auction-logic`)
5. Create

---

## 4️⃣ Fejlesztés és commit

Munka közben:

1. Nyisd meg a **Git Changes** panelt
2. Írd be a commit üzenetet
3. Kattints: **Commit All**

Ajánlott commit üzenet forma:

Add auction close logic
Fix user validation bug
Implement highest bid selection


---

## 5️⃣ Branch feltöltése (Push)

Commit után:

1. Kattints a **Push** gombra  
vagy  
2. Menü → **Git → Push**

Ha új branch-et hoztál létre, a Visual Studio automatikusan felajánlja a publish lehetőséget.

---

# 🔀 6️⃣ Pull Request létrehozása

Miután a branch feltöltésre került GitHubra:

1. Nyisd meg a GitHub repositoryt böngészőben
2. GitHub automatikusan felajánlja:
   **"Compare & pull request"**
3. Kattints rá
4. Ellenőrizd:
   - Base branch: `main`
   - Compare branch: `feature/...`
5. Adj meg címet és leírást
6. Kattints: **Create Pull Request**

---

## 7️⃣ Pull Request ellenőrzés és merge

Miután a Pull Request elkészült:

1. Ellenőrizd a változtatásokat
2. Ha minden rendben:
   - Kattints: **Merge Pull Request**
   - Confirm Merge

Ezután a módosítás bekerül a `main` branch-be.

---

## 8️⃣ Branch törlése (ajánlott)

Merge után:

- GitHubon: **Delete branch**
- Visual Studio-ban: törölheted a lokális branch-et is

---

## 9️⃣ Projekt frissítése merge után

Visual Studio-ban:

1. Válts vissza `main` branch-re
2. Menü → **Git → Pull**

---


#Logika

# 🧠 System Logic (Mermaid diagrams)

## 1) High-level user flow (Seller vs Buyer)

```mermaid
flowchart TD
    Start([Start]) --> Login[Login / Select user]
    Login --> Role{Role?}

    Role -->|Seller| SellerMenu[Seller Menu]
    Role -->|Buyer| BuyerMenu[Buyer Menu]

    %% Seller actions
    SellerMenu --> S1[List my items]
    SellerMenu --> S2[Add new item]
    SellerMenu --> S3[View item details]
    SellerMenu --> S4[Cancel auction (optional)]

    S2 --> CreateAuction[Create AuctionItem]
    CreateAuction --> OpenState[Set state = Open + close_time]
    OpenState --> SellerMenu

    S1 --> SellerMenu
    S3 --> SellerMenu
    S4 --> CancelAuction[Set state = Cancelled]
    CancelAuction --> SellerMenu

    %% Buyer actions
    BuyerMenu --> B1[List open auctions]
    BuyerMenu --> B2[Search auctions by title]
    BuyerMenu --> B3[View auction details]
    BuyerMenu --> B4[Place bid]
    BuyerMenu --> B5[My bids]
    BuyerMenu --> B6[Notifications / Inbox]

    B1 --> BuyerMenu
    B2 --> BuyerMenu
    B3 --> BuyerMenu
    B4 --> BidFlow[Bid validation + insert]
    BidFlow --> BuyerMenu
    B5 --> BuyerMenu
    B6 --> BuyerMenu

stateDiagram-v2
    [*] --> Open
    Open --> Closed : close_time reached (auto)
    Open --> Cancelled : seller cancels (optional)
    Closed --> [*]
    Cancelled --> [*]

sequenceDiagram
    participant U as User
    participant C as ConsoleApp
    participant D as PostgreSQL

    U->>C: bid <auctionItemId> <amount>
    C->>D: SELECT auction_state_id, close_time FROM auction_items WHERE id=...
    D-->>C: state + close_time
    C->>C: Validate state==Open AND close_time>now
    C->>D: SELECT MAX(amount) FROM bids WHERE auction_item_id=...
    D-->>C: currentHighest
    C->>C: Validate amount >= max(start_price, currentHighest+min_increment)
    C->>D: INSERT INTO bids(...)
    D-->>C: OK (bid_id)
    C-->>U: Bid accepted
flowchart TD
    A[Close auction] --> B[Load bids for auction]
    B --> C{Any bids?}
    C -->|No| D[Set state=Closed, winning_bid_id=NULL]
    C -->|Yes| E[Order: amount DESC, created_at ASC, bid_id ASC]
    E --> F[Select first row as winner]
    F --> G[Update auction_items: state=Closed, winning_bid_id, closed_at]
    D --> H[Create notifications (optional)]
    G --> H[Create notifications (winner + seller)]
    H --> I([Done])
sequenceDiagram
    participant T as Timer/Background task
    participant D as PostgreSQL
    participant C as CloseService

    T->>C: Tick (e.g. every 1s/5s)
    C->>D: SELECT open auctions WHERE close_time<=now()
    D-->>C: list of auction_item_id
    loop for each auction
        C->>D: BEGIN
        C->>D: SELECT winner bid (ORDER BY tie-break)
        C->>D: UPDATE auction_items SET state=Closed, winning_bid_id, closed_at
        C->>D: INSERT notifications (Pending)
        C->>D: COMMIT
    end

flowchart TD
    A[Auction closed] --> B[Insert Notification rows (Pending)]
    B --> C[Outbox worker polls Pending]
    C --> D{Send OK?}
    D -->|Yes| E[Update status=Sent, sent_at=now]
    D -->|No| F[attempt_count++, status=Failed or keep Pending]
    E --> G([Done])
    F --> G([Done])

flowchart LR
    A[Command / Timer event] --> B[Validate]
    B --> C[Persist change]
    C --> D[Write event log]

