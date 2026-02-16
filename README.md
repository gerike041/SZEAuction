# 🏗 SZEAuction – Console Auction System (.NET + PostgreSQL)

A **SZEAuction** egy .NET 8 alapú konzolos árverési rendszer, amely PostgreSQL adatbázisra épül. A projekt célja egy robusztus licitálási logika megvalósítása, kezelve az egyidejűséget és az automatizált lezárási folyamatokat.

---

## 👨‍💻 Fejlesztési folyamat (Visual Studio)

### 1️⃣ Repository klónozása
1. Nyisd meg a **Visual Studio-t**.
2. Válaszd a **Clone a repository** opciót.
3. Add meg a GitHub repository URL-jét.
4. Kattints a **Clone** gombra.

### 2️⃣ Saját branch használata (Kötelező)
**Soha ne dolgozz közvetlenül a `main` ágon!**
1. Nyisd meg a **Git Changes** panelt.
2. Kattints az aktuális branch nevére -> **New Branch**.
3. Elnevezési konvenció: `feature/funkcio-neve` (pl. `feature/bid-logic`).

### 3️⃣ Commit & Push
1. A módosítások után írj commit üzenetet (pl. `Add validation for bid amounts`).
2. Kattints a **Commit All** gombra.
3. Kattints a **Push** (felfelé mutató nyíl) gombra a szerverre küldéshez.

### 4️⃣ Pull Request (PR)
1. GitHubon kattints a **Compare & pull request** gombra.
2. Ellenőrizd: `base: main` <- `compare: feature/...`.
3. Sikeres jóváhagyás és Merge után Visual Studio-ban válts vissza `main`-re és nyomj egy **Pull**-t.

---

## 🧠 Rendszerlogika (Diagramok)

### 1) Felhasználói folyamatok


```mermaid
flowchart TD
    Start([Start]) --> Login[Belépés / Felhasználó választás]
    Login --> Role{Szerepkör?}

    Role -->|Eladó| SellerMenu[Eladói Menü]
    Role -->|Vevő| BuyerMenu[Vevői Menü]

    SellerMenu --> S1[Saját hirdetéseim]
    SellerMenu --> S2[Új aukció indítása]
    S2 --> CreateAuction[Aukció létrehozása]
    CreateAuction --> OpenState[Státusz = Open]

    BuyerMenu --> B1[Aktív aukciók listázása]
    BuyerMenu --> B4[Licitálás indítása]
    B4 --> BidFlow[Licit validálás + Mentés]

```
### 2) Licitálási folyamat (Szekvencia diagram)
```mermaid
flowchart TD
    A[Aukció lezárása] --> B[Licitek betöltése]
    B --> C{Van licit?}
    C -->|Nincs| D[Státusz=Closed, Nyertes=NULL]
    C -->|Van| E[Rendezés: Összeg DESC, Idő ASC]
    E --> F[Első rekord = Nyertes]
    F --> G[Update: auction_items (state, winning_bid_id)]
    G --> H[Értesítések küldése]
```
### 3) Aukció lezárása (Tie-break szabályok)
Ha az aukció lejár, a rendszer az alábbi prioritások szerint választ nyertest:
1. Legmagasabb összeg (Amount DESC).
2. Időrend (Aki előbb tette meg ugyanazt az összeget - CreatedAt ASC).
3. ID (Végső döntő - Id ASC)
    
```mermaid
flowchart TD
    A[Aukció lezárása] --> B[Licitek betöltése]
    B --> C{Van licit?}
    C -->|Nincs| D[Státusz=Closed, Nyertes=NULL]
    C -->|Van| E[Rendezés: Összeg DESC, Idő ASC]
    E --> F[Első rekord = Nyertes]
    F --> G[Update: auction_items (state, winning_bid_id)]
    G --> H[Értesítések küldése]
```
