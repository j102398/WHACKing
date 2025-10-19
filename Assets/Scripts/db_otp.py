import sqlite3
import sys
import random
import smtplib
import ssl
from email.message import EmailMessage
from email.mime.text import MIMEText
from cryptography.fernet import Fernet
import os
import shutil
import json



# --- CONFIGURATION & SECRETS ---

# WARNING: In a production application, these secrets must NOT be hardcoded.
# They should be loaded securely from environment variables or a secret manager.

# Encryption Setup (Fernet)
# Fernet Key: used to encrypt and decrypt the user data in the database
# Note: This key should be persistently stored, not generated on every run in a real app.
ENCRYPTION_KEY = Fernet.generate_key()
cipher = Fernet(ENCRYPTION_KEY)
print(f"INFO: Encryption Key generated (used for DB): {ENCRYPTION_KEY.decode()[:10]}...")

# Email Credentials (SMTP)
EMAIL_SENDER = 'smtp41290@gmail.com'
# Note: This is an App Password, not the main account password.
EMAIL_APP_PASSWORD = 'tmtq ukqn dsdr ozro'
EMAIL_SERVER = 'smtp.gmail.com'
EMAIL_PORT = 465

# --- INPUT HANDLING ---
try:
    # Requires the email to be passed as a command-line argument: python auth_system.py user@example.com
    target_email = sys.argv[1]
except IndexError:
    print("Error: No email argument provided.")
    print("Usage: python auth_system.py <target_email>")
    sys.exit(1)


# --- DATABASE SETUP & FUNCTIONS (SQLite + Fernet Encryption) ---

def get_db_connection():
    """Opens a plain SQLite database connection."""
    try:
        connection = sqlite3.connect('game.db')
        cursor = connection.cursor()
        return connection, cursor
    except Exception as e:
        print(f"Database connection error: {e}")
        sys.exit(1)


def setup_table(cursor):
    """Ensures the users table exists. Data columns are stored as BLOB (encrypted bytes)."""
    cursor.execute('''
    CREATE TABLE IF NOT EXISTS users (
        email BLOB PRIMARY KEY,
        username BLOB NOT NULL
    )
    ''')

#

def create_json_files(email):
    # Sanitize email to create a valid filename
    safe_email = email.replace('@', '_at_').replace('.', '_')

    # Get the script's directory (WHACKing/Assets/Scripts)
    script_dir = os.path.dirname(os.path.abspath(__file__))

    # Navigate to WHACKing directory (two levels up)
    whacking_dir = os.path.join(script_dir, '..', '..')

    # Navigate to UserData directory
    user_data_dir = os.path.join(whacking_dir, 'UserData')

    # Create UserData directory if it doesn't exist
    os.makedirs(user_data_dir, exist_ok=True)

    # Define file paths
    template_file = os.path.join(user_data_dir, 'map_state.json')
    user_file = os.path.join(user_data_dir, f'map_state_{safe_email}.json')

    # Check if user-specific file already exists
    if not os.path.exists(user_file):
        # Check if template file exists
        if os.path.exists(template_file):
            # Copy the template to create user-specific file
            shutil.copy(template_file, user_file)
        else:
            # If template doesn't exist, create a default empty structure
            default_data = {
                "map_data": [
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
                ],
                "house_placed": False,
                "house_location": None,
                "buildings_placed": False,
                "building_locations": []
            }
            with open(user_file, 'w') as f:
                json.dump(default_data, f, indent=4)



def insert_email(connection, cursor, email):
    """Encrypts and inserts user data."""
    try:
        # Generate a random username
        username = f"u{random.randint(100000000, 999999999)}"

        # Encrypt the sensitive data
        encrypted_email = cipher.encrypt(email.encode())
        encrypted_username = cipher.encrypt(username.encode())

        cursor.execute(
            'INSERT INTO users (email, username) VALUES (?, ?)',
            (encrypted_email, encrypted_username)
        )
        connection.commit()
        print(f"DB SUCCESS: User {username} inserted for email {email}")
        return True

    except sqlite3.IntegrityError:
        print(f"DB FAIL: Email '{email}' already exists.")
        return False
    except Exception as e:
        print(f"DB UNEXPECTED ERROR: {e}")
        return False


def show_users(cursor):
    """Helper for debugging: prints all decrypted users."""
    print("\n--- Current Encrypted Users in DB ---")
    cursor.execute('SELECT email, username FROM users')
    users_found = False
    for enc_email, enc_username in cursor.fetchall():
        try:
            email = cipher.decrypt(enc_email).decode()
            username = cipher.decrypt(enc_username).decode()
            print(f"User: {username} | Email: {email}")
            users_found = True
        except Exception as e:
            print(f"Could not decrypt row: {e}")
    if not users_found:
        print("No users found.")
    print("-------------------------------------")


# --- EMAIL FUNCTION (OTP) ---

def send_password(recipient_email):
    """Generates an OTP, sends it via SMTP, and returns the OTP."""
    otp = random.randint(100000, 999999)

    # 1. Create the HTML content
    html_content = f"""
    <html>
        <body>
            <div style="font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 5px;">
                <h2 style="color: #1a73e8;">Your One-Time Password (OTP)</h2>
                <p>Hello,</p>
                <p>Use the following code to complete your registration:</p>
                <div style="background-color: #f0f0f0; padding: 15px; border-radius: 4px; font-size: 24px; text-align: center; margin: 20px 0;">
                    <strong>{otp}</strong>
                </div>
                <p style="color: #888;">This code is valid for a short time only. Please do not share this code with anyone.</p>
                <p>Thank you for registering.</p>
            </div>
        </body>
    </html>
    """

    # 2. Create the email message
    email = EmailMessage()
    email['From'] = EMAIL_SENDER
    email['To'] = recipient_email
    email['Subject'] = "🔒 Your One-Time Password (OTP) for Registration"

    # Attach both plain text and HTML
    email.set_content(f"Your OTP is: {otp}. Please do not share this code.")
    email.add_alternative(html_content, subtype='html')

    # 3. Send the email
    context = ssl.create_default_context()
    smtp_server = smtplib.SMTP_SSL(EMAIL_SERVER, EMAIL_PORT, context=context)

    # Note: Requires a correct App Password for this to work
    smtp_server.login(EMAIL_SENDER, EMAIL_APP_PASSWORD)
    smtp_server.sendmail(EMAIL_SENDER, recipient_email, email.as_string())
    smtp_server.quit()

    return otp


# --- MAIN EXECUTION ---
if __name__ == "__main__":
    conn, curr = get_db_connection()
    setup_table(curr)

    # 1. Attempt to insert the user
    if insert_email(conn, curr, target_email):
        print(f"\nINFO: User created successfully. Sending OTP to {target_email}...")

        # Create JSON files for the new user
        create_json_files(target_email)

        # 2. Send the OTP
        try:
            otp_code = send_password(target_email)
            print(f"EMAIL SUCCESS: OTP email sent to {target_email}.")
            print(f"DEBUG: Generated OTP was: {otp_code}")
            #write the otp to otp.txt
            with open("otp.txt", "w") as otp_file:
                otp_file.write(str(otp_code))
        except smtplib.SMTPAuthenticationError:
            print("EMAIL FAIL: Authentication error. Check SENDER email and APP PASSWORD.")
        except Exception as e:
            print(f"EMAIL FAIL: Could not send email. Error: {e}")
            print("Note: If using Gmail, ensure you have enabled two-factor authentication and generated an App Password.")

    # 3. Clean up and display users
    show_users(curr)
    curr.close()
    conn.close()
    sys.exit(0)