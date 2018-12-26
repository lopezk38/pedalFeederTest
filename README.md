This is a quick and dirty feeder project for vJoy.

Most of this code is based off the vJoy SDK sample project. Some of the code is copy/pasted from the internet. Some of it was created my myself, lopezk38
As a result I do not claim to have created all the code in this project, but I am too lazy to go back through my internet history to track down all my sources since this was meant to be a quick and dirty project to get my personal setup up and running

I uploaded this to have a personal backup somewhere online. I will leave it publicly visible in case anyone happens to come across it looking for a basic example. However, this project is not intended to be used in any sort of non stopgap capacity.

This code is unoptimized. It is not commented well. It has poor variable names. It has hard coded values that should be parameterized. It is not an example of my personal skills. Do not take it in that capacity.

If anyone actually finds this, feel free to use it. I probably have a much better version of this in another github project though. You should use that instead.

What this is:

This is a program that I wrote to sit in between the vJoy driver and a set of modified Logitech G25 pedals I had laying around. This type of program is called a Feeder in the vJoy community.
It's job is to translate the data sent by the pedals to something the vJoy driver can understand. The vJoy driver takes that data and emulates a controller with it.

The hardware:

The Logitech G25 pedals are nothing more than a set of 3 potentiometers attached to the axis of each of their respective pedals. I modified the pedals to have an internal microcontroller. The microcontroller is equipped with an ADC which is hooked up to each potentiometer. The microcontrollers software is very simple. It opens a serial connection over USB with the host computer and then waits for the host computer to send one packet (1 byte per packet) of data. It does not care what is in this byte of data, the packet is simply a request data signal. It then asks its ADC for a reading of each pontentiometer. Then it sends 3 packets over serial to the computer, each containing one bytes worth of unsigned integer data describing the position of their respective potentiometer. It always sends the packets in this order: X axis, Y axis, Z axis. It sends no other data. If the host computer sends more packets while it is still performing the above actions, it ignores them. Potentiometer calibration is done in microcontroller software with mapping.

What it does:

It loops through a routine of fetching data from the microcontroller asyncronously, updating internal variables whenever the microcontroller replies with the data, mapping whatever data it recieved last from the microcontroller from 8 bit unsigned integer to 16 bit signed integer using a lookup table, and then sending that data to the vJoy driver. It's not super optimized - For example, it feeds the driver data every 20ms even if the pedals haven't replied yet or if the data hasn't changed and it remaps the data every cycle instead of at the data recieved event. It is a hack after all. The data between axis isn't synchronized either but that does't really matter since its probably close enough anyway unless the microcontroller freezes in between sending axis packets or something

Other details:

The program expects serial data on COM6 in 8-n-1 form. I probably shouldn't have hardcoded the COM port. Did I mention this is a hack?

As of first commit, the code was further hacked to change its output from raw X Y Z data to centered X and Y axis. The original X axis was changed to a positve offset on the centered X axis; this was also done to the Y axis. What was the Z axis was used to apply a negative offset to the centered X axis. The purpose of this was to simulate the left analog stick on a controller which is commonly used for movement in video games. The X axis is used for strafing left/right and the Y axis is used for moving forward. All the offsets are limited to a specific value so that the pedals can only be used for walking. Anything faster and you have to use WASD. This can be changed by adjusting the parameters in mapper1 and mapper2. This might change in later commits and I probably won't bother updating this readme since this isn't a serious project.