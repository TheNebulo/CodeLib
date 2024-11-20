import re

def check_password(password: str) -> bool:
	pattern = r'^(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?!.*\s).{8,20}$'
	return re.fullmatch(pattern, password) != None

def check_name(name: str, min_length: int, max_length: int) -> bool:
	pattern = fr'^[A-Za-z\s]{{{min_length},{max_length}}}$'
	return re.fullmatch(pattern, name.strip()) != None

def check_email(email: str) -> bool:
	pattern = r'[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}$'
	return re.fullmatch(pattern, email.strip()) != None