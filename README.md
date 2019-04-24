# Space Engineers ASCII Movies

This facility helps to play ASCII movies in Space Engineers, using only ingame scripts. It does not however help one create an ASCII movie. The usability and robustness of this facility could be better, but alas, it is not :D.

Internally it compresses a text movie file using a modified version of the LZW algorithm and outputs a base64 encoded binary file.

## How to play a movie ingame
To playback this movie inside the game you need to follow theese steps (also see Ingame.cs_):
  1. Create a programmable block, named "movie theater server"
  1. Create somewhere on the same grid an LCD screen, named "movie theater screen"
  1. Put base64 encoded data into its CustomData field.
  1. Put contents of [Ingame.cs_](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Ingame.cs_) into the code of the programmable block.
  1. Run the code

## How to create a compressed movie
Install vscode with nuget and omnisharp.

To create a base64 encoded compressed movie you need to follow theese steps (see Program.cs):
  1. Specify the alphabet of your text movie in [Alphabet.cs](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Alphabet.cs) and [Ingame.cs_](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Ingame.cs_). Currently the only alphabet supported is the alphabet of assciimation's Staw Wars. Alphabet is the set of distinct characters in your movie representation.
  1. Adjust paths and the alphabet in [Program.cs](https://github.com/grebenyukaa/se_ascii_movies/blob/master/Program.cs) to your own needs to create the encoded movie
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
