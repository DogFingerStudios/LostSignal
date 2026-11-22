Below are the revised tables with the additional **Resolution** column. I've kept the same eight MVP problems, with at least one representing each of the six problem types.

Severity indicates the impact on node operation, rather than the difficulty of the repair.

## **Table 1: MVP problems (8)**

These are the initial problems to implement in *Keep Driving*.

| Problem                   | Problem Type                 | Severity | Symptoms                                                                                        | Resolution                                                                                                              |
| ------------------------- | ---------------------------- | -------- | ----------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| Dirty solar panels        | Electrical and Power         | Minor    | Reduced charging efficiency; battery reserves gradually decline.                                | Visit node, inspect panels, clean accumulated dirt and debris using cleaning equipment.                                 |
| Failing battery           | Electrical and Power         | Moderate | Voltage drops overnight; intermittent shutdowns; low-battery warnings.                          | Test battery voltage and capacity; disconnect and replace failing battery.                                              |
| Damaged RF cable          | Radio and Signal             | Moderate | Weak or intermittent transmissions; fluctuating signal strength; increased signal loss.         | Inspect cables and connectors; identify damaged section; replace cable and verify signal quality.                       |
| Vegetation overgrowth     | Environmental Damage         | Minor    | Vegetation obstructs equipment or access paths; possible signal degradation.                    | Clear surrounding vegetation using appropriate cutting tools.                                                           |
| Antenna misalignment      | Mechanical and Structural    | Moderate | Reduced signal strength in certain directions; neighboring nodes report poor reception.         | Inspect antenna mounting; measure alignment; adjust orientation and secure mounting hardware.                           |
| Timing oscillator drift   | Radio and Signal             | Moderate | Increasing timing offset; inaccurate positioning; normal signal strength.                       | Connect diagnostic equipment; compare timing against reference; recalibrate oscillator or replace if defective.         |
| Controller failure        | Computer and Control Systems | Major    | Remote commands fail; telemetry disappears; transmitter may stop operating.                     | Connect diagnostic terminal; inspect power and controller status; reboot, restore configuration, or replace controller. |
| Battery health inspection | Routine Maintenance          | Minor    | Scheduled maintenance due; battery capacity or voltage trends indicate potential deterioration. | Visit node; measure battery voltage and capacity; inspect terminals; record results and replace battery if necessary.   |

## **Table 2: Master problem catalog**

The complete catalog contains 49 entries, including the eight MVP selections. Some represent failures, while others are preventive maintenance activities.

### **Electrical and Power**

| Problem                      | Problem Type         | Severity | Symptoms                                                                      | Resolution                                                                                          |
| ---------------------------- | -------------------- | -------- | ----------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| Dead battery                 | Electrical and Power | Major    | Node loses power when external generation is unavailable; transmission stops. | Replace battery; verify charging system and restore power.                                          |
| ★ Dirty solar panels         | Electrical and Power | Minor    | Reduced charging efficiency; declining battery reserves.                      | Clean panels; remove dust, pollen, and other debris.                                                |
| Damaged solar panel          | Electrical and Power | Moderate | Reduced or absent solar generation; charging warnings.                        | Inspect panel and electrical connections; repair wiring or replace damaged panel.                   |
| Corroded electrical contacts | Electrical and Power | Moderate | Intermittent electrical faults; voltage fluctuations; increased resistance.   | Disconnect power; clean or replace corroded terminals; apply corrosion protection.                  |
| Blown fuse                   | Electrical and Power | Moderate | One or more circuits lose power; equipment becomes unavailable.               | Diagnose underlying electrical fault; replace fuse with correct rating.                             |
| Faulty power regulator       | Electrical and Power | Major    | Unstable output voltage; unexpected shutdowns; electrical fault alarms.       | Measure regulator input/output; replace faulty regulator; verify stable voltage.                    |
| Generator out of fuel        | Electrical and Power | Moderate | Generator stops; battery reserves decline; eventual shutdown.                 | Transport fuel to node; refuel generator; restart and verify operation.                             |
| Generator mechanical failure | Electrical and Power | Major    | Generator fails to start or stalls; backup power unavailable.                 | Diagnose engine or mechanical fault; replace defective components or arrange generator replacement. |
| ★ Failing battery            | Electrical and Power | Moderate | Overnight voltage drops; intermittent shutdowns; reduced capacity.            | Test battery capacity and voltage; replace failing battery.                                         |

### **Radio and Signal**

| Problem                            | Problem Type     | Severity | Symptoms                                                                                     | Resolution                                                                                                        |
| ---------------------------------- | ---------------- | -------- | -------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| ★ Timing oscillator drift          | Radio and Signal | Moderate | Timing offset increases; positioning accuracy deteriorates; signal strength remains normal.  | Compare oscillator against calibrated reference; recalibrate or replace timing module.                            |
| Damaged antenna                    | Radio and Signal | Major    | Reduced transmission range; inconsistent reception; visible antenna damage.                  | Inspect antenna; replace damaged elements or entire antenna; verify performance.                                  |
| Loose RF connector                 | Radio and Signal | Minor    | Intermittent signal degradation; signal changes when cable is disturbed.                     | Inspect connectors; clean and tighten or replace defective connections.                                           |
| ★ Damaged RF cable / water ingress | Radio and Signal | Moderate | Weak signal; intermittent transmissions; increased signal loss.                              | Identify damaged cable using diagnostic equipment; replace cable and reseal connections.                          |
| Transmitter overheating            | Radio and Signal | Moderate | Rising temperature; reduced output; periodic protective shutdowns.                           | Inspect cooling system; clean filters and vents; replace faulty fans; verify temperature.                         |
| Frequency drift                    | Radio and Signal | Moderate | Carrier frequency deviates from specification; unreliable reception.                         | Measure output frequency; recalibrate frequency reference or replace oscillator circuitry.                        |
| Reduced output power               | Radio and Signal | Moderate | Signal strength decreases throughout coverage area; neighboring nodes report weak reception. | Measure transmitter output; inspect power amplifier and supply; replace defective components.                     |
| Interference                       | Radio and Signal | Moderate | Increased noise floor; poor signal quality; intermittent decoding failures.                  | Use spectrum diagnostics to identify interference; repair source or adjust channel/configuration where permitted. |
| Antenna misalignment (RF effects)  | Radio and Signal | Moderate | Direction-dependent signal loss; weak reception at neighboring stations.                     | Inspect alignment; physically reposition antenna; measure reception after adjustment.                             |

### **Environmental Damage**

| Problem                 | Problem Type         | Severity | Symptoms                                                                | Resolution                                                                                     |
| ----------------------- | -------------------- | -------- | ----------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| ★ Vegetation overgrowth | Environmental Damage | Minor    | Obstructed access; encroaching vegetation; possible signal degradation. | Cut bushes, grass, and branches around equipment and access paths.                             |
| Fallen tree             | Environmental Damage | Moderate | Access road blocked; equipment or cables potentially damaged.           | Remove fallen tree with appropriate equipment; inspect and repair affected infrastructure.     |
| Lightning damage        | Environmental Damage | Major    | Sudden equipment failure; surge alarms; damaged electronics.            | Inspect grounding and surge protection; replace damaged electronics; test system.              |
| Flooded enclosure       | Environmental Damage | Major    | Water intrusion; electrical faults; corrosion and shutdowns.            | Isolate power; drain and dry enclosure; replace damaged components; repair seals and drainage. |
| Dust accumulation       | Environmental Damage | Minor    | Reduced airflow; rising internal temperatures; clogged filters.         | Clean equipment interiors and vents; replace filters.                                          |
| Wind-damaged mast       | Environmental Damage | Major    | Antenna alignment changes; structural instability; degraded coverage.   | Assess structural damage; secure or replace damaged supports; realign antenna.                 |
| Erosion near foundation | Environmental Damage | Moderate | Exposed footings; ground displacement; structural warnings.             | Reinforce foundation area; restore soil; install drainage or erosion-control measures.         |
| Animal infestation      | Environmental Damage | Minor    | Nests or droppings; damaged insulation; intermittent electrical faults. | Safely remove or relocate animals; clean enclosure; repair damaged wiring; seal entry points.  |

### **Mechanical and Structural**

| Problem                    | Problem Type              | Severity | Symptoms                                                                | Resolution                                                                                  |
| -------------------------- | ------------------------- | -------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Loose mounting bolts       | Mechanical and Structural | Minor    | Equipment vibration; rattling; visible looseness.                       | Inspect mounting hardware; tighten bolts to specification; replace damaged fasteners.       |
| Rusted equipment enclosure | Mechanical and Structural | Minor    | Visible corrosion; deteriorating seals; increased water intrusion risk. | Remove corrosion; treat and repaint surfaces; replace severely damaged panels.              |
| Damaged access hatch       | Mechanical and Structural | Minor    | Hatch fails to close or lock; enclosure exposed to weather.             | Repair or replace hinges, latch, lock, or damaged hatch.                                    |
| Guy-wire tension problem   | Mechanical and Structural | Moderate | Mast leans or vibrates; abnormal cable tension; antenna movement.       | Inspect guy wires and anchors; use rated tensioning equipment or arrange specialist repair. |
| Cracked foundation         | Mechanical and Structural | Major    | Visible cracking; structural movement; mast alignment deteriorates.     | Assess structural integrity; arrange reinforcement or replacement of damaged foundation.    |
| Broken antenna rotator     | Mechanical and Structural | Moderate | Antenna cannot rotate; directional coverage limited.                    | Diagnose rotator motor or gearbox; replace faulty assembly; recalibrate orientation.        |
| Damaged fence              | Mechanical and Structural | Minor    | Broken fencing; unauthorized access risk; animals enter compound.       | Replace damaged posts, mesh, and gates; restore perimeter security.                         |
| Collapsed service platform | Mechanical and Structural | Major    | Maintenance access unavailable; visible structural failure.             | Restrict access; arrange reconstruction using replacement supports and platform materials.  |
| ★ Antenna misalignment     | Mechanical and Structural | Moderate | Antenna points away from intended direction; reduced signal reception.  | Measure orientation; adjust mounting brackets; align and secure antenna.                    |

### **Computer and Control Systems**

| Problem                       | Problem Type                 | Severity | Symptoms                                                                             | Resolution                                                                                  |
| ----------------------------- | ---------------------------- | -------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------- |
| ★ Controller failure          | Computer and Control Systems | Major    | Remote commands fail; telemetry stops; node may cease transmitting.                  | Diagnose controller and power supply; reboot, restore configuration, or replace controller. |
| Corrupted configuration       | Computer and Control Systems | Moderate | Unexpected behavior; incorrect operating parameters; configuration errors.           | Connect diagnostic terminal; validate settings; restore known-good configuration.           |
| Firmware failure              | Computer and Control Systems | Major    | Controller fails to boot; firmware errors; node unavailable.                         | Boot recovery mode; reinstall firmware; replace controller if recovery fails.               |
| Sensor malfunction            | Computer and Control Systems | Minor    | Implausible telemetry; contradictory readings; false alarms.                         | Compare readings against independent instrument; recalibrate or replace sensor.             |
| Network communication failure | Computer and Control Systems | Moderate | Remote management unavailable; local transmitter may continue functioning.           | Inspect communications interface, cables, and configuration; repair failed link.            |
| Incorrect node identifier     | Computer and Control Systems | Moderate | Receiver identifies wrong node; conflicting station records; unreliable positioning. | Verify node identity against registry; update identifier and provisioning configuration.    |
| Clock synchronization failure | Computer and Control Systems | Major    | Timing synchronization lost; positioning signals become unreliable.                  | Inspect timing reference and synchronization link; restore clock synchronization.           |
| Data storage full             | Computer and Control Systems | Minor    | Logging stops; storage warnings; historical diagnostics unavailable.                 | Retrieve or archive logs; remove obsolete data; repair storage or retention configuration.  |

### **Routine Maintenance**

| Problem                     | Problem Type        | Severity | Symptoms                                                                      | Resolution                                                                                        |
| --------------------------- | ------------------- | -------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Routine inspection          | Routine Maintenance | Minor    | Scheduled inspection due; no failure necessarily present.                     | Visit node; inspect equipment using checklist; record conditions.                                 |
| ★ Battery health inspection | Routine Maintenance | Minor    | Inspection due; capacity or voltage trends may reveal deterioration.          | Measure voltage and capacity; inspect terminals; record results and replace battery if necessary. |
| Antenna calibration         | Routine Maintenance | Minor    | Calibration interval exceeded; small alignment or signal deviations possible. | Measure antenna performance; adjust alignment and verify specifications.                          |
| Vegetation management       | Routine Maintenance | Minor    | Vegetation approaching equipment or access paths.                             | Clear vegetation before it obstructs access or equipment.                                         |
| Equipment cleaning          | Routine Maintenance | Minor    | Accumulated dirt, dust, and surface corrosion.                                | Clean equipment, vents, filters, and exposed surfaces.                                            |
| Firmware updates            | Routine Maintenance | Minor    | Approved firmware version available; node running older version.              | Connect terminal; back up configuration; install firmware and verify operation.                   |
| Survey verification         | Routine Maintenance | Minor    | Node coordinates or timing reference require periodic validation.             | Measure station position and timing against calibrated reference; update records if needed.       |
| Supply replenishment        | Routine Maintenance | Minor    | Emergency fuel, fuses, or replacement parts below target levels.              | Transport supplies; restock storage; update inventory records.                                    |

★ = Included in MVP.

## **MVP implementation priorities**

I'd implement the MVP failures in roughly this order, from simpler interactions to more involved systems:

| Order | Problem                   | Primary gameplay mechanic                  |
| ----- | ------------------------- | ------------------------------------------ |
| 1     | Dirty solar panels        | Cleaning interaction                       |
| 2     | Vegetation overgrowth     | Cutting/removing objects                   |
| 3     | Battery health inspection | Diagnostic measurement and reporting       |
| 4     | Failing battery           | Component replacement and inventory        |
| 5     | Damaged RF cable          | Visual inspection and cable replacement    |
| 6     | Antenna misalignment      | Physical adjustment and signal feedback    |
| 7     | Timing oscillator drift   | Instrument-based calibration               |
| 8     | Controller failure        | Multi-step electronic diagnosis and repair |

One design principle I'd strongly recommend: **the player should see symptoms before knowing the underlying problem**.

For instance, the receiver might report weak signal strength from HC-017. The cause could be a damaged RF cable, antenna misalignment, or another fault.

The player travels to the node, uses diagnostic tools, identifies the problem, and performs the appropriate repair.

That gives the game a genuine troubleshooting mechanic rather than a collection of predefined fetch quests.