# Ovikoodinlukijan dokumentaatiota

2024-05-06


## TWN3 Multi125 -RFID-lukijan asetukset

Aluksi laite näyttäytyy tietokoneelle (virtuaali)näppäimistönä. Tilan
muuttamiseksi pitää hommata valmistajan devpack:

<https://www.elatec-rfid.com/fileadmin/Files/TWN3-DevPack/TWN3DevPack439d.zip>

Devpackistä löytyy `TWNConfig.exe`, jolla vekotin laitetaan
konfiguraatiotilaan. Vasemman yläkulman valikosta pitää valita "*USB*" ja
painaa sen vieressä olevaa *Connect*-nappia. Sen kun tekee, niin
"näppäimistö" kadonnee ja tilalle tullee tuntematon laite. Devpäckistä löytyy
ajurit sille. Ajurien asennuksen jälkeen laite näkyy nimellä "*ELATEC GmbH TWN
Firmware Updater*" tai "*TWN Config Mode*" tai jotain sinne päin.

Sitten kun on saatu konfiguraatio-ohjelmalla yhteys "uuteen" laitteeseen (tätä
saattaa joutua yrittämään muutamaan kertaan ainakin eka kerralla), niin
vaihdetaan *Mode*-kohdasta tilaksi "*Intelligent Virtual COM Port*" ja
*Scripting*-kohdasta valitaan ohjelma, joka laitteelle annetaan
suoritettavaksi. Tässä pitäisi olla liitteenä `kustom.twn.c`, joka on muutoin
ihan sama kuin devpackin `standard.v3.twn.c`, paitsi että olen muuttanut yhden
komennon riviltä 155. Skripti kai pitää kääntää, ja sen jälkeen *Write Config*
-napista lähettää uudet asetukset laitteelle.

*Restart*-napilla poistutaan konfiguraatio-tilasta, ja laitteen pitäisi nyt
näkyä COM-porttiin kytkettynä vekottimena. Painakaa mieleen portin numero,
koska sitä tarvitaan jatkossa.


## Lätkien koodien selvittäminen

Jos laite on *Keyboard Emulation* -tilassa, niin ei tarvi kuin avata jokin
tekstieditori ja esitellä lätkiä laitteelle, jolloin se kirjoittaa koodin
aivan niin kuin se tulisi suoraa näppäimistöltä. Jostain syystä se tässä
tilassa haluaa esittää koodin nimenomaan 10-järjestelmän lukuna. Tämänpä vuoksi
muokkasin laitteen standard-skriptiä niin, että sekin esittää ne samassa
muodossa.

Jos laite puolestaan on aiemman osion mukaisesti asetettu COM-portti-tilaan,
niin seuraavassa on konsteja, joilla voi lukea dataa COM-portista.

### Linuxissa

Ainakin Ubuntussa tuntui minulla riittävän komento

```bash
cat /dev/serial/by-id/usb-OEM_RFID_Device__COM_-if00
```

jossa tuon viimeisen osan sain autocompletellä ihan vain tabulaattoria
naputtelemalla, koska ei ollut kuin yksi *serial*-laite. Sitten ei tarvitse
kuin heilutella lätkiä laitteelle.

### Windowsissa PowerShellillä

Fiksumpiakin tapoja varmaan olisi, mutta yksi keino on käyttää PowerShelliä. Ei
ainakaan tarvi ruveta asentelemaan mitään ylimääräisiä ohjelmia. PowerShellissä
(ja `cmd`:ssä) komennolla `mode` saa
[listattua COM-portit](https://superuser.com/questions/835848/how-to-view-serial-com-ports-but-not-through-device-manager).
Siitä näkee, mitä seuraavaan kannattaa laittaa `COM3`:n paikalle. Sitten
seuraavanlaisilla
[komennoilla](https://devblogs.microsoft.com/powershell/writing-and-reading-info-from-serial-ports/)
pitäisi saada luettua lukijan viestejä.

```ps
$p = new-Object System.IO.Ports.SerialPort COM3
$p.NewLine = "`r"
$p.Open()
$p.ReadLine()
```

Tuota viimeistä komentoa kun toistelee, niin se lukee laitteelta aina yhden
"rivin" kerrallaan, kunhan sille lätkiä heilutellaan. Ei olisi varmaankaan
pahitteeksi, jos lopuksi vielä kutsuttaisiin `$p.Close()`, mutta se ei vaikuta
pakolliselta.

En ole PowerShelliä ikinä opetellut, mutta nyt näyttäisi siltä, että
PowerShellissä pystyy em. konstilla
[luomaan .NET-objekteja](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/new-object?view=powershell-7.4&viewFallbackFrom=powershell-6>)
ja käyttelemään niitä komentorivillä. Mielenkiintoista. Tuo `NewLine` pitää
[vaihtaa](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_special_characters?view=powershell-7.4),
koska defaulttina TWN3 käyttää jostain syystä *carriage returniä*, enkä
halunnut mennä sitä muuttelemaan.

### Vasitulla apuohjelmalla

Koska PowerShell-komentojen toistelu on vaivalloista, kirjoittelin
Windows-puolen helpottamiseksi pienen konsoliohjelman, joka tekee suunnilleen
saman kun tuo Linux-komento.


## Tietokanta

Liitteenä pitäisi olla sql-skripti, joka luo tietokantaan 4 taulukkoa ja yhden
*stored proceduren*.

`Holidays`-taulukossa pitäisi olla listattuna vuoden 2024 vapaat ja
vaillinaiset päivät opiskelijan maan mukaan. ***Tarkistakaa, että ovat
oikein.*** **Lomakalenteri ei ollut aivan minuutintarkka eikä siitä käynyt
ilmi, minä päivinä on brunssi ja milloin normaali lounas jne.**

Kolme muuta taulukkoa ovat aluksi tyhjiä.

### Taulukot

(Enimmäkseen kopioitu englanninkielisistä kommenteista.) En listannut tähän
sarakkeita tai niitten tyyppejä tms., koska sellaiset asiat selviävät kuitenkin
paremmin suoraan scriptistä lukemalla.

- `Users`
    - Taulukko seurattavista ihmistä
    - country needed to determine which days are holidays etc.
    - `status` is a bunch of flags:
        - The least significant bit indicates whether user is currently logged
          in.
            - 0 means out and 1 means in.
        - The second least significant bit indicates whether the user was
          automatically logged out.
        - The third least significant bit indicates whether the user was
          logged using the web app.
        - I thought I was being clever by "caching" data, but
          [allegedly](https://www.databasedesign-resource.com/denormalization.html)
          keeping redundant data like this is unwise.
- `Tags`
    - RFID tags in use and whom they are currently assigned to
- `Loggings`
    - Table of all log in/out events
    - `new_status` is the status obtained because of the event in question.
- `Holidays`
    - Special days for each country
    - Negative value indicates that the whole day is off or "distance".
    - Non-negative value means there are that many fewer minutes of work that
      day.


## Lätkänlukuohjelmasta

### Komentoriviparametrit

Ensimmäinen parametri on COM-portin numero. Tähän pitää laittaa se luku, joka
tuolla aiemmin saatiin selville. (oletusarvo 3)

Toinen on viive millisekunteina, kuinka kauan ohjelma odottelee inputtia, ennen
kuin tyhjää ruudun ja tarkastaa kellonajan. (oletusarvo 15000)

Jos haluaa käyttää muuta kuin oletusarvoja, niin esim. Task Schedulerissä on
kohta, johon voi laittaa command line argumentteja. Toinen vaihtoehto olisi
laittaa ne battiin, jolla käynnistää ohjelman.

### Toiminnasta

Ohjelma on vielä sen verran yksinkertainen, etten suotta viitsinyt ruveta
tekemään omia classeja sun muita asioille, jotka eivät sellaisia tarvi. Koko
homma koostuu yhdessä ja samassa `Program`-luokassa olevista kentistä ja
metodeista.

Ohjelma alkoi ihan konsoliapplikaationa, ja kai se sitä vieläkin on, mutta
jouduin `.csproj`-tiedostoa editoimalla hakkeroimaan siihen vähän Windows Forms
-ominaisuuksia, jotta saisin sen automaattisesti laittamaan itsensä full
screeniin.

Pääsilmukassa on oikeastaa vain kaksi funktiota:

- `WaitForInputLoop()`: Lue sarjaportista koodi
- `LogUserInput()`: Tee koodin mukainen merkintä tietokantaan

Kieltäytyy logaamasta väärään kellonaikaan (21--06). En ole ainakaan vielä
pannut tarkistusta lomapäiville ja viikonlopuille.

Tällä hetkellä ehkä vähän typerästi tuossa lukufunktiossa on silmukka, jonka
sisään on kertynyt semmoista tavaraa, joka kuuluisi pääsilmukkaan. Ne
kannattaisi siirtää sieltä ulos.
Lukemisfunktio hoitaa sarjaportista lukemisen lisäksi myös seuraavat tehtävät:

- Putsaa ruudun sen jälkeen, kun `SerialPort.ReadLine()` heittää
  `TimeoutException`:in.
- Kun kellon tunti tulee 21, kirjaa ulos kaikki käyttäjät, jotka ovat vieläkin
  mukamas sisäänkirjaantuneina, sekä asettaa statukseen bitin, josta näkee,
  että kyseessä oli automaattinen uloskirjautuminen.

    Tämä käyttää `DateTime.Now`:ta, ettei tarvisi yhtenään olla tietokannalta
    utelemassa asioita. **Varmistakaa, että kellot ovat suurin piirtein
    synkassa.**

`LogUserInput()` tekee seuraavia asioita:

- Katsoo tietokannasta, kuka on annetun koodin käyttäjä.
- Katsoo `Users`-taulukosta nykytilanteen ja päivittää sen sekä tekee
  `Loggings`-taulukkoon merkinnän, josta ilmenee mentiinkö ulos vai sisään sekä
  pvm ja kellonaika.
- Näyttää edellisen kohdan tiedot ruudulla sekä käyttäjän maan mukaisen
  tervehdyksen. **Joku kielitaitoinen tarkastakoon nämäkin.**
- Laskee paljonko pitäisi tehdä hommia tällä viikolla ja paljonko on
  toistaiseksi tehty ja paljonko pitäisi tämän päivän loppuun mennessä olla
  tehtynä.
- Näyttää em. tuloksia mukamas oikein värikoodattuina.
    - punainen, jos on päivä yli kahta tuntia vaille valmis
    - vihreä, jos on jo tehty tarpeeksi tälle päivälle
    - keltainen, jos on siltä väliltä

Lähdekoodissa on sen verran kommentteja, ettei varmaan ole tarvetta tässä sen
tarkemmin selitellä yksityiskohtia.

Huomauttamisen arvoinen asia ehkä on, että noissa tietokantahommissa ei saa
olla kuin yksi `SqlDataReader` tms. auki kerrallaan. Pari kertaa heitti herjaa,
kun silmukassa luin tietokannasta tietoja ja niitten tietojen perusteella
yritin tehdä myös jotain muuta tietokannalle. Piti järjestellä silmukat uusiksi
taikka käyttää `SqlDataAdapter`:ia, joka lukee kaikki tiedot yhdellä
rykäisyllä.


## Web äpistä / admin-portaalista

ASP.NET Core MVC:llä tehty. Laiskaan tyyliin enimmäkseen Visuan studion
scaffoldeilla se on suurimmalta osin luotu. Pyrin lisäilemään kommentteja
niihin paikkoihin, joissa on joitain omia keksintöjä.
