### Speed limits

| Inspector setting | Current | What it does |
|---|---:|---|
| Max Forward Kmh | 90 | Top speed going forward without Shift. |
| Max Boosted Forward Kmh | 100 | Top speed going forward with Shift held. |
| Max Reverse Kmh | 25.2 | Top speed in reverse without Shift. |
| Max Boosted Reverse Kmh | 30 | Top speed in reverse with Shift held. |

### Acceleration and braking

| Inspector setting | Current | What it does |
|---|---:|---|
| Acceleration | 8 | How hard the truck pulls away from a stop. Higher gives a quicker launch. |
| Boosted Acceleration | 10.5 | How hard it pulls away with Shift held. |
| Top Speed Acceleration | 2 | How strongly it keeps accelerating as it approaches its speed limit. Higher makes the last stretch to top speed quicker. |
| Handbrake Deceleration | 6 | How hard `Space` slows forward or backward motion. Higher shortens the stop. |
| Direction Change Deceleration | 11 | How hard the truck brakes when you press the opposite pedal. This is the main setting for `S` braking while moving forward. |
| Direction Change Speed | 0.5 m/s | How close to stopped you must be before the opposite pedal starts driving in the new direction. Lower keeps it in the braking phase longer. |
| Rolling Drag | 0.55 | The baseline slowdown when you coast or hold the handbrake. Higher means the truck rolls less after you lift off the gas. |
| Speed Squared Drag | 0.0018 | Extra slowdown while coasting or handbraking, strongest at high speed. Raise this if the truck carries too much speed with your foot off the gas. |

### Grip

| Inspector setting | Current | What it does |
|---|---:|---|
| Lateral Grip | 7 | How quickly the tires pull the truck out of a sideways slide during ordinary driving. Higher feels more planted. |
| Handbrake Lateral Grip | 1.3 | Sideways grip during a fast handbrake slide. Higher cleans up the slide sooner. Lower lets the rear swing out longer. |
| Handbrake Slide Speed | 5 m/s | Roughly 18 km/h. Above this threshold, the handbrake can reduce sideways grip and increase turning authority. Raise it if slow handbrake turns slide too easily. |
| Off Route Traction | 0.76 | Off-road multiplier for acceleration, braking, sideways grip, and steering. `0.76` means those effects are 76% as strong as on a route. Raise it if the truck feels too loose or slow to stop off-road. |

### Steering

| Inspector setting | Current | What it does |
|---|---:|---|
| Steering Response | 5 | How quickly steering ramps up when you press `A` or `D`, and returns to center when you release. Higher feels snappier. |
| Low Speed Yaw Rate | 82°/s | How quickly the truck can turn at low speed once it has enough movement. Higher makes tight turns easier. |
| High Speed Yaw Rate | 24°/s | How quickly the truck can turn at high speed. Lower makes small steering corrections less twitchy. |
| Yaw Fade Start Speed | 6 m/s | About 22 km/h. Steering starts transitioning from the low-speed turn rate here. |
| Yaw Fade End Speed | 36 m/s | About 130 km/h. The transition finishes here. Your ZIL tops out at 100 km/h, so it never reaches the full high-speed setting. Lower this if you want steering to settle down sooner. |
| Full Steering Speed | 4 m/s | About 14 km/h. Below this, turn authority ramps up from zero as the truck starts moving. Lower gives you more turning power while creeping. |
| Handbrake Yaw Multiplier | 1.25 | Gives up to 25% more turning authority during a fast handbrake turn. Higher makes it easier to swing the truck around. |

### Ground contact

| Inspector setting | Current | What it does |
|---|---:|---|
| Low Speed Contact Limit | 7 m/s | About 25 km/h. Below this, an already grounded truck gets extra leeway to follow small dips. |
| Low Speed Contact Tolerance | 0.12 m | The extra leeway at low speed. Higher smooths out small bumps and dips, but can make the truck feel more glued down. |
| Contact Tolerance | 0.003 m | The tighter ground-contact leeway at speed or when airborne. Higher makes the truck follow the ground more readily. Lower makes it easier to leave the ground over a crest. |
