# Package Imports
from flask import g, flash, redirect, url_for, render_template
from flask_login import UserMixin
import os, psycopg2, time, inspect

# User Class
class User(UserMixin):
    def __init__(self, id):
        self.id = id # For passwords, generate_password_hash from werkzeug.security is highly reccommended

# Failed Connection Class (used for providing a different user class to handle database connection errors)
class ConnectionFail():
    def __init__(self, error, template):
        self.error = error # Used for the error object
        self.template = template # Used to hold the name of the template to open upon error

# Database Config
def get_credentials() -> dict:
    """Fetches postgres database credentials from the system environment.

    Returns:
        dict: Required postgres credentials
    """
    return {
        'dbname': os.getenv('dbname'),
        'user': os.getenv('user'),
        'password': os.getenv('password'),
        'host': os.getenv('host'),
        'port': os.getenv('port')
    }

# Database Intialisation
def get_connection():
    retry_count = 3 # max_database_reconnect_count
    delay_seconds = 0.5 # database_reconnect_attempt_delay
    for attempt in range(retry_count):
        try:
            if 'db_conn' not in g:
                g.db_conn = psycopg2.connect(**get_credentials())
            return g.db_conn
        except psycopg2.OperationalError as e:
            if attempt < retry_count - 1:
                time.sleep(delay_seconds)
            else:
                raise e

# Error Handling
def handle_error(error, url: str = None, critical = False):
    """Internal function for handling database errors

    Args:
        error (any): An error object
        url (str, optional): A url to redirect to if the error isn't critical. Defaults to None, when it would redirect back to the initial endpoint that caused the error.
        critical (bool, optional): Whether the error is critical and should redirect to a specfic offline template. Defaults to False.
    """
    # You can log errors here
    if critical: return render_template("connection_failed.html"), 500 # Can be changed to any html page
    else:
        flash('Data server connection failed', category='error')
        if not url:
            file = os.path.splitext(os.path.basename(inspect.stack()[1].filename))[0]
            func = inspect.stack()[1].function
            url = f"{file}.{func}"
        return redirect(url_for(url))

# Query Execution
def execute_queries(*queries, fetch_one = False, fetch_all = False, commit = True): # Example query tuple would be ('SELECT * FROM accounts WHERE id = %s;', (str(id),))
    """Execute a list of postgres queries

    Args:
        *queries (list<tuple>): A list of tuples, structured as (query_string_to_execute, placeholders)
        fetch_one (bool, optional): Whether to fetch a single response back from the database and return it. Defaults to False.
        fetch_all (bool, optional): Whether to fetch a all valid responses back from the database and return them. Defaults to False.
        commit (bool, optional): Whether to commit a transaction at the end of the executed query. Defaults to True.
    """
    response = None
    conn = get_connection()
    with conn.cursor() as cur:
        for query_tuple in queries: cur.execute(query_tuple[0], query_tuple[1])
        if fetch_all: response = cur.fetchall()
        if fetch_one: response = cur.fetchone()
        if commit: conn.commit()
    return response