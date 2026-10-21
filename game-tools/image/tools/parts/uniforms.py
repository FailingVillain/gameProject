"""Uniform material sets (names from voxel.MATERIALS). A unit json may name one ("uniform": "us_od")
or give a dict with any of these keys to override."""

UNIFORMS = {
    "us_od":    dict(jacket="od", trousers="khaki", legging="canvas", boot="leather", belt="canvas", hand="skin"),
    "us_hbt":   dict(jacket="hbt", trousers="hbtp", legging="hbtp", boot="leather", belt="leather", hand="skin"),
    "us_scout": dict(jacket="scoutj", trousers="khaki", legging="canvas", boot="leather", belt="canvas", hand="skin"),
    "us_officer": dict(jacket="feldgrau", trousers="lkhaki", legging="lkhaki", boot="leather", belt="leather", hand="skin",
                       trouser_stripe=True),       # officer: infantry trouser khaki + team-colour outer seam stripe
    "ww1_khaki": dict(jacket="feldgrau", trousers="lkhaki", legging="puttee", boot="leather", belt="leather", hand="skin",
                      collar=True, puttees=True),      # field-grey stand-collar tunic, khaki trousers, puttees, ankle boots (generic Central-European WWI)
    # infantry-reserve: very dark navy tunic / trousers / wraps with thin fixed red piping (collar, placket, cuffs, outer trouser seam)
    "navy_old": dict(jacket="navy_old", trousers="navy_old", legging="navy_old_leg", boot="leather", belt="leather", hand="skin",
                     collar=True, puttees=True, puttee_seam="navy_old_legd", trouser_stripe=True, stripe_mat="piping_red", piping="piping_red"),
}
