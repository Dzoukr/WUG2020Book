# Ukázkový projekt WUG Days 2020

## Minimální požadavky

* The [.NET Core SDK](https://www.microsoft.com/net/download) 3.1 nebo vyšší.
* [npm](https://nodejs.org/en/download/) package manager.
* [Node LTS](https://nodejs.org/en/download/).

## Jak spustit aplikaci
Pokud spouštíte aplikaci **poprvé**, musíte nainstalovat lokální tools pomocí příkazu:

```bash
dotnet tool restore
```

Ke spuštění serveru a klientské části ve "watch módu", použijte následující příkaz:

```bash
dotnet fake build -t run
```

Poté otevřete browser na adrese `http://localhost:8080`.

## SAFE Stack dokumentace
Chcete-li se dozvědět více informací o Azure Stacku a jeho komponentách, podívejte se na [oficiální SAFE Stack dokumentaci](https://safe-stack.github.io/docs/).
