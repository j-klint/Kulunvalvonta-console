# Kulunvalvonta-console

Pieni harjoitustekele, jonka tein osana
[Nordin opintoja](https://utbnord.se/fin/koulutukset/koulutustyypit/ammatillinen-koulutus/130--pohjoismainen-sovelluskehittaja).

Tämä lukee TWN3-RFID-lukijalta ja tekee merkintöjä SQL-tietokantaan.

Tuossa [seliseli.md:ssä](https://github.com/j-klint/Kulunvalvonta-console/blob/main/seliseli.md)
on vähän tarkemmin selitetty, miten tätä käytetään.

**Huom.** että tuossa 2024-06-14 tehdyssä päivityksessä muutin tietokannan
käyttämään UTF-8:aa, mutta tällä hetkellä (2024-08-12) käytössä olevassa
versiossa on vielä käytössä Latin1-codepage tai mikä siinä defaulttina
tuleekaan. Jos haluaa seuraavaa tietokantaa luodessa pysytellä defaultissa,
niin pitäisi olla melko ilmeistä, mikä osa `luontiskripti.sql`:stä pitää
jättää pois. (Se on se osa, jossa puhutaan collatesta ja utf8:sta.)

Tälle on apuriohjelma, jolla pääsee nettisivukäyttöliittymän kautta tonkimaan
tietoja:<br>
https://github.com/j-klint/Kulunvalvomo-mvc
