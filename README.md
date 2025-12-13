# Simai Chart Generator
An automatic chart generator for Simai/maimai based on audio analysis. Developed in C# (WinForms), this tool can read MP3 audio and automatically generate charts for all difficulty levels from EASY to Re:MASTER, outputting them in a maidata.txt format readable by the Simai simulator.

## Key Feature
* Clean Visuals: Significantly optimized the generation logic of Slides and Touches, adding a cooling mechanism to prevent "noodle screens" or visual obstruction.
* Dynamic Style: Each generation includes a random style factor (sparse, standard, dense), allowing for different experience patterns even for the same song.

## Multilingual Support
Supports dynamic switching of the UI language and automatically remembers user preferences:
* English
* Traditional Chinese
* Simplified Chinese
* Japanese

## How to Use
![](https://i.imgur.com/PQU9eMC.png)
* Load Audio: Click "Browse" to select your .mp3 file.
* Set Background: (Optional) Select a background image (.jpg/.png).
* BPM Setting: The program will automatically detect the BPM. If it is inaccurate, you can check "Manual Input" to correct it.
* Select Difficulty: Check the difficulty level you want to generate (EASY ~ Re:MASTER).
* Click "Generate Rate".
* Preview the generation log to confirm the estimated level (Lv.) and combo count.
* If you are not satisfied, you can click "Generate" again (the result will change slightly each time).
* Click "Export Files" and select the save folder. The program will automatically create a song folder and output maidata.txt.


## Development
* IDE: Visual Studio 2019 / 2022
* Framework: .NET Framework 4.7.2

Dependencies:
* NAudio (Used for audio processing and BPM detection)


## Installation and Build:
* Clone this project to your local machine.
* Open the .sln file using Visual Studio.
* Restore NuGet Packages.
* Build the solution.

## License 
*This project is open source under the MIT License. The generated charts are for learning and communication purposes only and should not be used for commercial purposes.