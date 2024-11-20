import phonenumbers

def check_phone_number(phone_number: str) -> bool:
	try:
		parsed_number = phonenumbers.parse(phone_number, None)
		return phonenumbers.is_valid_number(parsed_number)
	except: return False

def format_phone_number(phone_number: str) -> str: 
	parsed_number = phonenumbers.parse(phone_number, None)
	formatted_number = phonenumbers.format_number(parsed_number, phonenumbers.PhoneNumberFormat.INTERNATIONAL)
	formatted_number_with_spaces = formatted_number.replace("-", " ")
	return formatted_number_with_spaces