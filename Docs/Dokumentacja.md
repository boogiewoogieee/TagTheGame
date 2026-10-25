# TagTheGame — dokumentacja projektu (wersja robocza)

Stan opisany poniżej dotyczy wersji z połączeniem bezpośrednim. Przed oddaniem projektu należy uzupełnić oznaczone miejsca i sprawdzić instrukcję na wydanej kompilacji Windows.

## Uruchomienie

### Gotowa gra na Windows

1. Rozpakuj cały katalog kompilacji. Plik `TagTheGame.exe` musi pozostać razem z katalogiem `TagTheGame_Data` i pozostałymi plikami potrzebnymi do uruchomienia gry. Przy kopiowaniu lub pakowaniu pomiń katalog `TagTheGame_BackUpThisFolder_ButDontShipItWithYourGame`.
2. Uruchom `TagTheGame.exe` na dwóch komputerach w tej samej sieci. Do rozgrywki potrzebne są dwie osoby; jeden komputer jest gospodarzem (hostem), drugi klientem.
3. Gospodarz wybiera **Multiplayer**, wpisuje nazwę gracza na ekranie dołączania, klika **Host Lobby**, a na następnym ekranie **Create Lobby**. Gra pokazuje jego adres sieciowy. Domyślny port połączenia to `7777` (UDP).
4. Drugi gracz wybiera **Multiplayer**, wpisuje swoją nazwę oraz adres IP gospodarza na ekranie dołączania i klika **Join Lobby** na tym samym ekranie. Na jednym komputerze do próby lokalnej można użyć `127.0.0.1`.
5. Oboje wybierają **Ready**. Gospodarz wybiera **Start match**. Gra wczytuje arenę obu graczom.

Przy pierwszym uruchomieniu Windows Firewall może poprosić gospodarza o zezwolenie na dostęp do sieci; należy zezwolić dla sieci prywatnych. Jeżeli gracze są w różnych sieciach, gospodarz musi zapewnić osiągalny adres i możliwość połączenia przez port UDP `7777` (np. przez odpowiednią konfigurację sieci). Błędny adres można anulować przyciskiem **Cancel**. W trakcie meczu `Esc` otwiera menu z opcjami powrotu do gry, wyjścia do menu i zamknięcia gry. Otwarcie tego menu nie zatrzymuje czasu rundy.

**Do uzupełnienia po wdrożeniu listy gier online:** kroki utworzenia i wyszukania serwera, wymagania dotyczące internetu oraz zachowanie przy niedostępności usługi. Planowana próba dwóch kopii gry na jednym komputerze używa różnych nazw profili, np. `TagTheGame.exe -profile a` i `TagTheGame.exe -profile b`; wymaga potwierdzenia po wdrożeniu tej funkcji.

### Kompilacja z projektu

Projekt otwiera się w Unity `6000.6.3f1`. W menu Edytora wybierz **TagTheGame → Build Windows** (albo **Build Windows (Development)**). Wynik znajduje się w `Builds/Windows/TagTheGame.exe`. Można też uruchomić Unity w trybie wsadowym z `-batchmode -quit -projectPath <katalog-projektu> -executeMethod BuildWindows.BuildFromCommandLine`. Sceny włączone w Build Settings są kompilowane w kolejności: `Bootstrap`, `MainMenu`, `Arena_01`.

## Zasady i funkcje

- Gra jest lokalną sieciową rywalizacją dwóch graczy. Gospodarz prowadzi sesję i rozstrzyga stan meczu; drugi gracz łączy się jako klient. Każdy steruje własną postacią z kamerą trzecioosobową.
- Na początku rundy bomba trafia do jednego z graczy. Kontakt z przeciwnikiem z bliskiej odległości przekazuje bombę; krótki czas odnowienia zapobiega natychmiastowemu odbiciu. Wspólny licznik zapalnika nie resetuje się po podaniu.
- Po upływie zapalnika przegrywa gracz trzymający bombę. Wybuch ma efekt dźwiękowy i wizualny oraz reakcję postaci. Wynik rundy i stan punktów są pokazywane przed kolejną rundą.
- Mecz kończy się, gdy jeden gracz wygra dwie rundy. Ekran wyników pokazuje zwycięzcę, punkty, czas trzymania bomby i liczbę podań. Gospodarz może rozpocząć kolejny mecz lub wrócić do lobby. Odłączenie przeciwnika kończy mecz walkowerem.
- Lobby pokazuje imiona i gotowość obu graczy. Mecz może uruchomić tylko gospodarz, kiedy oboje są gotowi. Po rozpoczęciu meczu dołączanie jest zablokowane.
- Dostępne są menu główne, ekran lobby, HUD meczu, menu pod `Esc` oraz ekran wyników. Opcja **Next level** jest przygotowana w kodzie, ale ukryta, dopóki nie zostanie dodana druga arena.

## Wykorzystane zasoby i pakiety

Wersje pakietów podano według wspólnego `Packages/manifest.json`, a licencje według plików `LICENSE` dostarczonych z pakietami. Wbudowane moduły Unity (`com.unity.modules.*`) są częścią silnika i nie są tu wyliczane osobno. Dla zasobów oznaczonych „do weryfikacji” trzeba ustalić dokładną stronę źródłową oraz warunki użycia przed oddaniem dokumentacji.

| Pakiet Unity | Wersja | Zastosowanie / źródło i licencja |
|---|---:|---|
| Universal Render Pipeline | 17.6.0 | Renderowanie; Unity Package Manager, Unity Companion License. |
| Netcode for GameObjects | 2.13.3 | Synchronizacja sieciowa; Unity Package Manager, Unity Companion License. |
| Multiplayer Play Mode | 3.0.0 | Testy wielu graczy w Edytorze; Unity Package Manager, Unity Package Distribution License. |
| Input System | 1.20.0 | Sterowanie; Unity Package Manager, Unity Companion License. |
| Cinemachine | 6.6.0 | Kamera; Unity Package Manager, Unity Companion License. |
| AI Navigation | 2.0.14 | Pakiet obecny w projekcie; Unity Package Manager, Unity Companion License. |
| Test Framework | 1.8.0 | Testy Edytora; Unity Package Manager, Unity Companion License. |
| Timeline | 6.6.0 | Pakiet obecny w projekcie; Unity Package Manager, Unity Companion License. |
| Visual Scripting | 1.9.12 | Pakiet obecny w projekcie; Unity Package Manager, Unity Package Distribution License. |
| uGUI | 2.6.0 | Zależność części interfejsu zasobów; Unity Package Manager, Unity Companion License. |
| Version Control | 2.13.6 | Narzędzie Edytora; Unity Package Manager, Unity Package Distribution License. |
| Rider Editor i Visual Studio Editor | 3.0.38 i 2.0.26 | Integracje IDE; Unity Package Manager, licencja MIT. |

| Zasób | Lokalizacja / wykorzystanie | Źródło i licencja |
|---|---|---|
| First Person + Third Person — Character Controllers (Starter Assets) | `Assets/THRAX/Starter Assets`; sterowanie postacią | [Unity Asset Store, Unity Technologies](https://assetstore.unity.com/packages/3d/characters/first-person-third-person-character-controllers-196526); niestandardowa Unity Companion License. |
| Low Poly Character Collection 3 | `Assets/THRAX/Hodaart`; modele postaci | Źródło i licencja do weryfikacji (niepotwierdzony identyfikator Asset Store: 388758). |
| Toon Gas Station | `Assets/THRAX/Toon Gas Station`; otoczenie areny | Źródło i licencja do weryfikacji (niepotwierdzony identyfikator Asset Store: 155369). |
| Human Melee Animations FREE | `Assets/THRAX/Kevin Iglesias`; animacje | Źródło i licencja do weryfikacji (niepotwierdzony identyfikator Asset Store: 165785). |
| Stylized Explosives AAA | `Assets/THRAX/Stylized Explosives`; rekwizyty | Identyfikator pakietu Asset Store: 287118; strona i licencja do weryfikacji. |
| Cinematic Explosions FREE | `Assets/explosioneffect`; efekt wybuchu | Unity Asset Store, Mirza Beig, identyfikator pakietu 257086; licencja do weryfikacji. |
| 3D Modern Menu UI | `Assets/SlimUI`; szablony interfejsu | [Unity Asset Store, SlimUI](https://assetstore.unity.com/packages/tools/gui/3d-modern-menu-ui-116144); licencja do weryfikacji. |
| Grafika tła menu (`wp9382134-landscape-illustration-wallpapers.jpg`) | `Assets/HUD/UI/Images`; tło menu głównego | Źródło i licencja do ustalenia z osobą, która zaimportowała plik. |
| TextMesh Pro | `Assets/TextMesh Pro`; tekst interfejsu | Zasób Unity; dokładne warunki do weryfikacji. |
| Pozostałe importy zespołu | `Assets/Bombasset`, `Assets/TunnelAsset`, `Assets/Sign street`, `Assets/SFX` | Autorstwo, pochodzenie i licencje do uzupełnienia przez osoby importujące. |

## Podział prac w zespole

Poniższą tabelę należy uzupełnić na podstawie rzeczywistych zadań i zatwierdzić wspólnie z zespołem.

| Osoba | Zakres prac | Wykonane elementy / dowody |
|---|---|---|
| [imię i nazwisko] | [do uzupełnienia] | [do uzupełnienia] |
| [imię i nazwisko] | [do uzupełnienia] | [do uzupełnienia] |
| [imię i nazwisko] | [do uzupełnienia] | [do uzupełnienia] |
| [imię i nazwisko] | [do uzupełnienia] | [do uzupełnienia] |
