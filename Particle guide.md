# Requirements
* KSP Parttools
* Unity 2019.4.18f1
* Artistic Vision
# Setup
Create a project in unity standard rendering pipeline, then follow part tools instruction
# Creating the effect
Add a gameobject to the scene, then another one inside, both should be at the same location, with 1,1,1 as the scale
then:
* Add Partools component to the outer one
* Add KSP Particle Emitter component to the one inside
* Create a material using ONLY the shaders avialable in KSP directory
# Limitations
* No particle can Puff outward (You can increase the size)
* 1 KSP emitter per .mu written
* You cannot make a glowing particle turns into black smoke using gradients, Itll disappear, Use 2 particles for this case
# KSP Particle Emitter
* **Material:** the material the particle will use
* **Size:** Size of particle in metre
* **Sizegrow:** Make particle grow overtime (chaotic, set around 0.1)
* **Energy:** Lifetime of each particle in seconds
* **Emit:** Amount of particle to emit per second
* **Random velocity:** Generate a velocity inbound of the vector specified, can be used to make expanding particle, but looks bad
* **Angular Velocity:** How fast it spins
* **Max size:** Max size onscreen, 1 for whole screen
# Exporting
* Click set up material first
* Go into parttools component
  * Check Show material and click compile
  * Set the path and name
  * Set export type
  * Click Export
* Done!

# Debugging
* **Errors in Unity:** You did not make the particle properly
* **Particle not appearing:** You did not make the particle properly, Remember to set up particle and materials, And the emitter must be under a gameobject which has partools
* **Particle showing but small:** Try going into the bomb's cfg and set bdnuke yield to 20 kilotons (Normal BDA+)
* **Particle as big as planets:** Set size grow to 0 and yield to 20 kt
* **Particles showing for a split second:** Lower velocity, see if it helps
* **I did everything right, but it is not showing:** KSP additive cannot be used with any color that is even slightly dark, try setting alpha in the animate color to 255 and color all of them white
* **It doesn't even explode:** Mistake in either Unity or configuration file of the part

