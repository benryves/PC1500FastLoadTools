# PC-1500 Fast Load Tools

Convert data to and from wave files for transfer to a PC-1500 running Fast Load
by Logi'Stick.

## Introduction

The Sharp PC-1500 can save or load programs and other data via the CE-150
cassette interface and printer.

If you wish to transfer data to or from a modern PC you can use Sharp
PocketTools to convert between BASIC program listings (in text form, .txt/.bas),
raw binary data (.img/.bin), tape images (.tap) or wave files (.wav).

This works well, but can be rather slow due to the format used by Sharp to store
on the tape.

One solution to this low speed is Fast Load by Logi'Stick. This is a machine
language program that can be loaded onto the PC-1500 and provides routines to
save and load programs and variable data at roughly fourteen times the speed of
the native format.

Fast Load is fairly bare-bones (I've not found a way to save or reload machine
code programs using it, only BASIC programs or variables, nor have I found a way
to verify a program once transferred) but the high speed still makes it an
attractive tool.

## Installing Fast Load

Fast Load is made up of three files:

* FAST-RELO: a BASIC program that loads the other two files.
* FAST-RESERVE: a reserve program that redefines the soft keys.
* FAST-LOAD: the Fast Load machine code routines.

If you have the three files as separate recordings (rather than the original
tape) then you'll need to play them back in the order above when loading onto
the PC-1500.

You will need to know where to load FAST-LOAD to, and this will depend on the
memory layout of your particular machine and whether you have any memory modules
or other machine language programs loaded.

The start of RAM can be found with `PEEK &7863*256`. The reserve program will be
loaded to here, and is 197 bytes long.

After this, you will need to decide how much RAM to reserve for FAST-LOAD and
any other machine language programs you may wish to load. FAST-LOAD is 540 bytes
long, so at the bare minimum you'll need 540+197=737 bytes for the reserve
program and FAST-LOAD.

Assuming you're only going to be loading in FAST-LOAD, first move the start of
BASIC's memory up by 737 bytes:

    NEW PEEK &7863*256+737

Now execute `CLOAD` and start playing FAST-RELO. This will take a little over a
minute to load, and once done execute `RUN`.

This will immediately start loading the reserve program, so play FAST-RESERVE.
After a few seconds the computer should beep and display a `@:` prompt.

Here you need to enter the address you're loading FAST-LOAD to. Assuming no
other machine language programs are being loaded this would be best placed
immediately after the reserve program, so the load address would be the start of
RAM plus 197 bytes for the reserve program. Enter

    @:PEEK &7863*256+197

This will now start loading FAST-LOAD from tape into memory, so play that file.

Once complete the computer will display "UN INSTANT", patch the reserve program
and FAST-LOAD programs to relocate them to the address that you loaded them to,
then display a copyright notice and beep twice. You're now ready to use
Fast Load.

## Using Fast Load

Fast Load can be accessed via reserve keys F1 (!) and F5 (%) when using reserve
program groups II and III. F1 saves programs (group II) or variables (group II)
and F5 loads programs (group II) or variables (group III).

Unlike the native save and load routines there doesn't appear to be any way to
to store a file name or type, nor is there a way to redefine the load or save
address or length. As such, I don't think it can be used to store machine
language programs (only BASIC programs or variables) and you must be careful
when loading or saving.

There is a three-byte checksum to check for data integrity, at least, however
there isn't a routine to test loading after a save to verify the data.

One other issue of note is that Fast Load seems to be rather fussy about signal
phase. The phase when loading must be reversed when compared to the phase when
saving, otherwise it will not load the data.

Cassette recorders (usually) reverse the phase, in my experience, but after
recording to a PC I need to invert the waveform before playing it back.

## Fast Load Tools

Two tools are provided:

* fwav2bin: Extracts binary data from Fast Load recording.
* fbin2wav: Converts binary data into audio that can be loaded into Fast Load.

These tools are designed to complement PocketTools and so accept similar command
line arguments. One notable limitation is that unlike PocketTools they have no
capability to detokenise/tokenise BASIC programs to and from text, so if you
wish to extract a BASIC program listing from a Fast Tools-saved recording you
will first need to use fwav2bin to extra the binary data, then use PocketTools
to convert that binary data into a BASIC program listing.

### File Formats

Three main file formats are supported:

* img/bin: Raw binaries. PocketTools differentes between binary BASIC programs
  (img) and machine language programs/raw data (bin) but no such differentiation
  is made here.
* tap: This corresponds to the data bytes stored on the tape. The first two
  bytes store the length of the data, followed by the data itself (including an
  FF terminator). The final three bytes store a three-byte checksum. Note that
  as the tape format used by Fast Tools is very different to the format used by
  the native cassette routines, a tape file generated by these tools is
  incompatible with a tape file generated by PocketTools.
* wav: Audio wave file.

### Usage Examples

Converting a recording of a slow-loading program into a fast-loading one:

    wav2bin --type=img prog-slow.wav
    fbin2wav prog-slow.img prog-fast.wav
	
Converting a recording of a fast-loading program into a slow-loading one:

    fwav2bin prog-fast.wav
    bin2wav --name=PROG prog-fast.img prog-slow.wav

Converting a BASIC listing into a fast-loading wave file:

    bas2img prog.bas
    fbin2wav prog.img prog-fast.wav
	
Converting a fast-loading wave file into a BASIC listing:

    fwav2bin prog-fast.wav prog.img
    bin2wav prog.img prog-slow.wav
    wav2bin prog-slow.wav prog.bas