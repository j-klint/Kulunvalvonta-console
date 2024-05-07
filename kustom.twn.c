//
//    File: standard.twn.c
//    Date: 04/11/2009
// Version: 3
//
// Purpose:
//
// This is the standard script for TWN3 readers, which is installed
// as default script on TWN3 readers. This script can be run on any
// type of TWN3 reader without modification.
// 
// Feel free to modify this program for your specific purposes!
//
// V1:
// ---
// - Initial release
//
// V2:
// ---
// - Extended protocol specification (see below)
//
// V3:
// ---
// - Save ID before modifying it.
//
// ****************************************************************************
// ******                      PROTOCOL DESCRIPTION                      ******
// ****************************************************************************
//
// The standard script implements a unidirectional communication to the host.
// This means, that there are no commands available, which can be sent from the
// host to the TWN3 reader ("device").
//
// All communication from the device to the host is based on lines of ASCII
// characters, which are terminated by carriage return (<CR>). Please note,
// that there is a option in the configuration of TWN3, which will append a
// line feed (<LF>). This option is turned off by default.
//
// ----------------------------------------------------------------------------
// Startup Message
// ----------------------------------------------------------------------------
//
// There is a difference between a USB device and (physical!) V24 device. The
// V24 is sending a startup message to the host, which identifies the verions of
// the firmware. Here is an example of how such a startup message might look:
//
// ELA GM4.02<CR>
//       ++++----- Firmware Version
//      +--------- Transponder Family (see below)
//     +---------- Firmware (G = standard version)
// ++++----------- Product identification (always identical)
//
// Assignment of Characters to Transponder Families:
//
//   'N': Multi125
//   'M': Mifare
//   'I': HID iClass
//   'H': HID Prox
//   'A': Legic
//   'D': Inditag
//   'S': MultiISO
//
// ----------------------------------------------------------------------------
// Identification of a Transponder
// ----------------------------------------------------------------------------
//
// Once a transponder has been swiped over the reader, the ID of this reader is
// sent to the host. The ID is sent as a line of hex characters or decimal
// characters (HID Prox only). The ID of the transponder has a variable length
// depending on the type of the transponder. A typical ID looks as follows:
//
// 12345678<CR>
//
// The maximum length of an ID is 8 bytes, which lead to 16 ASCII character,
// when displayed in hex notation.

#include <sys.twn.h>

const byte MAXIDBYTES = 8;
const byte MAXIDBITS = MAXIDBYTES*8;

byte ID[MAXIDBYTES];
byte IDBitCnt;
byte TagType;

byte LastID[MAXIDBYTES];
byte LastIDBitCnt;
byte LastTagType;

void main()
{
    // Make some noise at startup at minimum volume
    Beep(BEEPSUCCESS);
    // Set maximum volume
    SetVolume(4);
    // A V24 device is sending the version at startup
    if (GetConnection() == V24)
    {
        HostSendVersion();
        HostSendChar('\r');
    }
    // Turn on green LED
    LEDSet(GREEN,ON);
    // Turn off red LED
    LEDSet(RED,OFF);
    // No transponder found up to now
    LastTagType = TAGTYPE_NONE;
    while (TRUE)
    {
        // Search a transponder
        if (TagSearch(ID,IDBitCnt,TagType))
        {
            // Is this transponder new to us?
            if (TagType != LastTagType || IDBitCnt != LastIDBitCnt || !CompBits(ID,LastID,MAXIDBITS))
            {
                // Save this as known ID, before modifying the ID for proper output format
                CopyBits(LastID,0,ID,0,MAXIDBITS);
                LastIDBitCnt = IDBitCnt;
                LastTagType = TagType;
                
                // Yes! Sound a beep
                Beep(BEEPHIGH);
                // Turn off the green LED
                LEDSet(GREEN,OFF);
                // Let the red one blink
                LEDSet(RED,BLINK);
                
                // Send the ID in our standard format
                if (TagType == TAGTYPE_HIDPROX)
                {
                    // Send HID ID in decimal format
                    if (IDBitCnt < 45)
                    {
                        if (IDBitCnt > 32)
                        {
                            // Show ID without the paritys at start
                            CopyBits(ID,0,ID,IDBitCnt-32,31);
                            HostSendDec(ID,31,0);
                        }
                        else
                        {
                            // Show ID without the paritys at start and end
                            IDBitCnt -= 2;
                            CopyBits(ID,0,ID,1,IDBitCnt);
                            HostSendDec(ID,IDBitCnt,0);
                        }
                    }
                    else
                        // Show ID in plain long format
                        HostSendDec(ID,IDBitCnt,0);
                }
                else
                {
                    // Send ID with appropriate number of digits
                    HostSendDec(ID,IDBitCnt,0);
                }
                HostSendChar('\r');
            }
            // Start a timeout of two seconds
            StartTimer(0,20);
        }
        if (TestTimer(0))
        {
            LEDSet(GREEN,ON);
            LEDSet(RED,OFF);
            LastTagType = TAGTYPE_NONE;
        }
    }
}
