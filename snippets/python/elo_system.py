# Random and unfinished, worth keeping for future references

import math

def calculate_upset_multiplier(rating_diff, base=10, strength=800):
    return math.exp((rating_diff / strength) * math.log(base))

def dynamic_max_change(rating_diff, base_k=32, max_factor=8):
    dynamic_change = base_k * calculate_upset_multiplier(rating_diff)
    return min(dynamic_change, base_k * max_factor)

def update_elo(winner_rating, loser_rating, k_factor=48):
    winner_rating = max(1, winner_rating)
    loser_rating = max(1, loser_rating)

    rating_diff = loser_rating - winner_rating
    max_change = dynamic_max_change(rating_diff, k_factor)

    if rating_diff > 0:
        upset_multiplier = calculate_upset_multiplier(rating_diff)
        change = round(k_factor * upset_multiplier)
    elif rating_diff == 0:
        change = round(k_factor * 1.15)
    else:
        upset_multiplier = 1 / calculate_upset_multiplier(abs(rating_diff))
        change = round(k_factor * upset_multiplier)

    change = min(change, max_change)
    change = max(round(k_factor * 0.85), change)

    winner_new_rating = max(1, round(winner_rating + change))
    loser_new_rating = max(1, round(loser_rating - change))

    return winner_new_rating, loser_new_rating