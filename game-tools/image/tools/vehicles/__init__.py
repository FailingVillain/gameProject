"""Vehicle sprite pipeline (hull sheet + separately rotating turret sheet).

Modules
  vehcommon   resolution profiles (hull 288x288 / turret 240x240 at the main scale, native 1/2 and 1/4 LODs),
              vehicle materials, mount / pivot maths, compositing, sheet + layout writers, numeric checks
  renault_ft  Renault FT style light tank: hull + turrets {"mg": Hotchkiss MG, "37mm": short 37 mm cannon}
  armored_car 1930s four-wheel armored car: hull + turret {"mg"}
  loader      units/<name>.json with "kind": "vehicle" -> Vehicle (hull/turret frame renderers)
Entry point: image/make_vehicle.py
"""
