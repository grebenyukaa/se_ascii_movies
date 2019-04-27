# Space Engineers ASCII Movies

This facility helps to play ASCII movies in Space Engineers, using only ingame scripts. It does not however help one create an ASCII movie. The usability and robustness of this facility could be better, but alas, it is not :D.

Internally it compresses a text movie file using a modified version of the LZW algorithm and outputs a base64 encoded binary file separated into 64kb chunks with unique identifiers.

## How to store a movie ingame
The maximum size of data, that can be stored by CustomData field of IMyTerminalBlock is 64kb, or 64000 symbols. Usually your compressed movie size will far exceed this limit. Therefore to store a movie ingame you need some kind of a datastorage, e.g. an isolated group of IMyTerminal blocks (a pack of batteries on small grid for example), which you will fill with your movie's chunks. This facility splits the output encoded movie into as many chuncks, as needed, and stores it in movies/output/movie_name.base64.{GUID}.txt format.

So, to store it ingame you need to follow theese steps:
  1. Open movies/output/ folder, look for movie_name.base64.{GUID}.txt files.
  1. For each GUID of a file of the aforementioned pattern:
      1. Cretate a terminal block (e.g. small battery), name it so that it contains that GUID (e.g. volume GUID).
      1. Put the text from the file to the CustomData field of the block 

## How to play a movie ingame
So you have already stored your movie in your data storage and want to play it. To achieve this you need to follow theese steps (also see [Ingame.cs_](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Ingame.cs_)):
  1. Create a programmable block, named "movie theater server"
  1. Create somewhere on the same grid 3 LCD screens, named "movie theater screen left", "movie theater screen center" and "movie theater screen right". Set them in "text and images" mode.
  1. Put GUIDs of your storage "volumes" into the programmable block's CustomData. One GUID per row. Keep in mind, that order matters, so I recommend to just copy-paste [those](https://github.com/grebenyukaa/se_ascii_movies/blob/master/src/Program.cs#L39) omitting quotation marks.
  1. Look for movies/output/alphabet.txt file.
  1. Put contents of this file into [this variable in Ingame.cs_](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Ingame.cs_#L353). This is a base64 encoded alphabet of the LZW encoding/decoding algorithm.
  1. Put contents of [Ingame.cs_](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Ingame.cs_) into the code of the programmable block.
  1. Run the code.

## How to create a compressed movie
Install vscode with nuget and omnisharp.

To create a base64 encoded compressed movie you need to follow theese steps (see [Program.cs](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Program.cs)):
  1. Adjust paths in [Program.cs](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Program.cs) to your own needs to create the encoded movie
  1. Run the program and wait until it finishes.

## Frame format
Frame consists of several strings, separated with a '\\n' character.
To see an example frame see movies/*. The frame format specification can be found [here](http://www.asciimation.co.nz/asciimation/ascii_faq.html))

The format itself is as follows:

entity | type 
--------------------|----------------
frame playback time | int
N strings of the frame data | string[]

  
## Internals
The ingame script decodes data in blocks across multiple ticks. The blocksize of 2048 bytes seems to be close to the perfomance cap of a user script on a single tick in Space Engineers. Currently different frame delays are not actually supported and are ignored in playback algorithm.

## Examples
This repo contains an already prepared Star Wars movie from [asciimation](http://www.asciimation.co.nz/). To play it, follow the steps from the section "How to play a movie ingame".
