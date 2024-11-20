from flask import flash, redirect, render_template, url_for, jsonify, request, abort
from flask_login import login_required, current_user
from functools import wraps
from psycopg2 import OperationalError
import database

# Stops functions from continuing if database is unreachable (or if authentication requirements aren't met)
def requires_online_database(requires_auth = False, allow_post_auth_return = True, use_response_codes = False, error_redirect: str = None, critical_error = False):
	"""Decorator that will confirm a valid database connection (and completed authentication) before proceeding with the request

	Args:
		requires_auth (bool, optional): Whether an authenticated user is required. Defaults to False.
		allow_post_auth_return (bool, optional): Whether this page can be redirected back to after authentication. Defaults to True.
		use_response_codes (bool, optional): Whether to use response codes as a request response instead of redirects. Defaults to False.
		error_redirect (str, optional): Upon database error, where should the request be redirected. Defaults to None.
		critical_error (bool, optional): Whether the error is critical. Relevant for database module. Defaults to False.
	"""
	def decorator(function):
		@wraps(function)
		def decorated_function(*args, **kwargs):
			try: 
				if current_user.__class__ == database.ConnectionFail: return render_template(current_user.template), 500 # Database refers to a module called database.py
							
				elif requires_auth and (not allow_post_auth_return or use_response_codes):
					if current_user == None: 
						if use_response_codes: jsonify("User not authenticated"), 403
						flash("Please log in to access this page")
						return redirect(url_for(".authenticate"))
					elif not current_user.is_authenticated: 
						if use_response_codes: jsonify("User not authenticated"), 403
						flash("Please log in to access this page")
						return redirect(url_for(".authenticate"))
					else:
						return function(*args, **kwargs)

				elif requires_auth: return login_required(function)(*args, **kwargs)
				else: return function(*args, **kwargs)
				
			except OperationalError as e: 
				if use_response_codes: jsonify("Data server connection failed"), 500
				return database.handle_error(e, error_redirect, critical_error)
		return decorated_function
	return decorator


# Usage example

"""
# Home Page Route
@blueprint.route('/settings')
@requires_online_database(requires_auth=True, allow_post_auth_return=False, error_redirect=".authenticate")
def home():
	return render_template("settings.html", user=current_user)
"""

# Content size limiter
def limit_content_length(max_length: int):
	"""Limit the maximum size of file content delivered in request. Upon exceeding the limit, the request is aborted with a 413 status code.

	Args:
		max_length (int): Maximum size of bytes allowed (measured in request's content length header).
	"""
	def decorator(f):
		@wraps(f)
		def wrapper(*args, **kwargs):
			cl = request.content_length
			if cl is not None and cl > max_length: abort(413)
			return f(*args, **kwargs)
		return wrapper
	return decorator