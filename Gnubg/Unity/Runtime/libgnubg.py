import base64
import gzip
import json
import logging
import os

import gnubg

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


def compress_data(data):
    """Compress data using gzip and encode it with base64."""
    try:
        comp_data = gzip.compress(data.encode("utf-8"))
        return base64.b64encode(comp_data).decode("utf-8")
    except Exception as e:
        print(f"Error compressing data: {e}")
        return None


def decompress_data(data):
    """Decode from base64 and decompress data using gzip."""
    try:
        decoded_data = base64.b64decode(data)
        decompressed_data = gzip.decompress(decoded_data)
        return decompressed_data.decode("utf-8")
    except Exception as e:
        print(f"Error decompressing data: {e}")
        return None


def read_game_content_from_file(file_path):
    """Read game content from a local file with improved error handling."""
    try:
        with open(file_path, "r", encoding="utf-8") as file:
            return file.read()
    except FileNotFoundError:
        print(f"The file {file_path} does not exist.")
        return None
    except IOError as e:
        print(f"An error occurred when reading from the file {file_path}: {e}")
        return None


def delete_local_file(file_path):
    """Delete the specified file."""
    try:
        os.remove(file_path)
        # print(f"File '{file_path}' was successfully deleted.")
    except OSError as e:
        print(f"Error: {e.strerror} - {e.filename}")


def write_dict_to_json_file(data, m_ref, output_dir):
    """
    Write dictionary to JSON file with better resource management. If the file exists,
    read its content and merge it with the new data before writing it back.

    Parameters:
    - data (dict): Data to be written or merged into the file.
    - m_ref (str): Reference name for the match, used to define the file path.
    """
    filename = os.path.join(output_dir, f"{m_ref}.json")

    # Ensure the directory exists
    os.makedirs(os.path.dirname(filename), exist_ok=True)

    try:
        # Write (or overwrite) the file with the merged data
        with open(filename, "w", encoding="utf-8") as file:
            json.dump(data, file, indent=4)

    except IOError as e:
        print(f"Failed to write to {filename}: {e}")


def new_gnubg_match(m_length: int, m_ref: str):
    """
    Creates a new GNU Backgammon match and saves it to a specified directory.

    Parameters:
    - match_length (int): The length of the backgammon match.
    - match_ref (str): The reference name for the match, which is used to name the directory and match file.

    Returns:
    - None: The function prints the match details.
    """
    # Create the directory for the match
    m_dir = f"/tmp/{m_ref}"
    os.makedirs(m_dir, exist_ok=True)

    # Create a new match and save it
    if m_length == 0:
        gnubg.command(f"new session")
    else:
        gnubg.command(f"new match {m_length}")


def load_match(gnubg_id: str, jacoby: str, variation: str):
    """
    Loads a match from a file and then deletes the file.

    Parameters:
    - m_ref (str): Reference identifier for the match, used to construct the file path.
    """
    gnubg.command(f"set variation {variation}")
    gnubg.command(f"set jacoby {jacoby}")
    gnubg.command(f"set gnubgid {gnubg_id}")


def save_match(m_ref: str, output_dir: str):

    # Collect game identifiers and match information directly from the gnubg module
    data_dict = {
        # "sgf": compressed_data,  # Compressed match data in SGF format
        # board
        # "board": gnubg.board(),
        # calcgammonprice
        #  "gammonprice": gnubg.calcgammonprice(),
        # cfevaluate
        # "cfevaluate": gnubg.cfevaluate(),
        # classifypos
        # command
        # cubeinfo
        # "cubeinfo": gnubg.cubeinfo(),
        # dicerolls
        # eq2mwc
        # eq2mwc_stderr
        # errorrating
        # evalcontext
        # evaluate
        # findbestmove
        # "bestMove": gnubg.findbestmove(),
        # getevalhintfilter
        # gnubgid
        # "gameId": gnubg.gnubgid(),
        # hint
        # "hint": gnubg.hint(),
        # "luckRating": gnubg.luckrating(0),  # TODO: how does this work?
        # match
        # "matchInfo": gnubg.match(),
        # matchchecksum
        # "matchCheckSum": gnubg.matchchecksum(),
        # matchid
        # "matchId": gnubg.matchid(),
        # met
        # movetupletostring
        # mwc2eq
        # mwc2eq_stderr
        # navigate
        # nextturn
        # parsemove
        # posinfo
        # "posInfo": gnubg.posinfo(),
        # positionbearoff
        # positionfrombearoff
        # positionfromid
        # positionfromkey
        # positionid
        # "positionId": gnubg.positionid(),
        # positionkey
        # "positionKey": gnubg.positionkey(),
        # rolloutcontext
        # setevalhintfilter
        # updateui
    }

    try:
        data_dict["cfevaluate"] = gnubg.cfevaluate()
    except Exception as err:
        print(f"cfevaluate error: {err}")
        data_dict["cfevaluate"] = None

    try:
        data_dict["cubeinfo"] = gnubg.cubeinfo()
    except Exception as err:
        print(f"cubeinfo error: {err}")
        data_dict["cubeinfo"] = None

    try:
        data_dict["hint"] = gnubg.hint()
    except Exception as err:
        print(f"hints error: {err}")
        data_dict["hint"] = None

    try:
        data_dict["bestMove"] = gnubg.findbestmove()
    except Exception as err:
        print(f"bestMove error: {err}")
        data_dict["bestMove"] = None

    # Write the collected data to a JSON file using the match reference as the file name
    write_dict_to_json_file(data_dict, m_ref, output_dir)


def format_move_list_string(input_string):
    # Step 1: Remove square brackets
    cleaned_string = input_string.replace("[", "").replace("]", "")

    # Step 2: Remove extra spaces around commas to ensure numbers are not split
    cleaned_string = cleaned_string.replace(" , ", ",")

    # Step 3: Replace commas with a single space
    cleaned_string = cleaned_string.replace(",", " ")

    # Step 4: Correct remaining multiple spaces that might occur inside numbers
    cleaned_string = " ".join(cleaned_string.split())

    # Print the resulting string
    return cleaned_string


def configure_hint_evaluation():
    """
    Issue `set evaluation …` commands based on uppercase env-vars
    with explicit defaults matching our Pydantic model.
    """
    # 1) Which context: chequerplay or cubedecision
    ctx = os.getenv("EVAL_TYPE", "chequerplay")

    # 2) Always use the neural-net evaluator
    gnubg.command(f"set evaluation {ctx} type evaluation")

    # 3) Plies (default 2)
    plies = os.getenv("PLIES", "2")
    gnubg.command(f"set evaluation {ctx} evaluation plies {plies}")

    # 4) Prune (default off)
    prune = os.getenv("PRUNE", "true").lower()
    prune_flag = "on" if prune in ("true", "1") else "off"
    gnubg.command(f"set evaluation {ctx} evaluation prune {prune_flag}")

    # 5) Noise (default 0.0)
    noise = os.getenv("NOISE", "0.0")
    gnubg.command(f"set evaluation {ctx} evaluation noise {noise}")

    # 6) Deterministic (default on)
    det = os.getenv("DETERMINISTIC", "true").lower()
    det_flag = "on" if det in ("true", "1") else "off"
    gnubg.command(f"set evaluation {ctx} evaluation deterministic {det_flag}")

    # 7) Cubeful (default true)
    cubeful = os.getenv("CUBEFUL", "true").lower()
    cubeful_flag = "on" if cubeful in ("true", "1") else "off"
    gnubg.command(f"set evaluation {ctx} evaluation cubeful {cubeful_flag}")


def update_match(match_ref, gnubg_id, jacoby, variation, action, output_dir):
    """
    Load the match, configure hint eval if needed, then save state.
    """
    # 1) restore game
    load_match(gnubg_id, jacoby, variation)

    # 2) apply hint params
    if action == "hint":
        configure_hint_evaluation()

    # 3) capture hint + state
    save_match(match_ref, output_dir)


def main():
    # Retrieve the 'MATCH_REF' environment variable that specifies the match reference identifier.
    # If 'MATCH_REF' is not set, default to 'default_ref'.
    match_ref = os.getenv("MATCH_REF", "default_ref")

    # Retrieve the 'MATCH_LENGTH' environment variable to define the length of the match.
    # Convert this to an integer. Default to 0 if 'MATCH_LENGTH' is not provided.
    match_length = int(os.getenv("MATCH_LENGTH", 0))

    # Retrieve the 'VARIATION' environment variable to determine the type of backgammon game variation.
    # If not set, defaults to the standard variation.
    variation = os.getenv("VARIATION", "standard")

    # Retrieve the 'JACOBY' environment variable which indicates whether the Jacoby rule is active.
    # Convert the string to lowercase and compare to "true" to set it as a boolean.
    jacoby = os.getenv("JACOBY", "false").lower() == "true"

    # Retrieve the 'ACTION' environment variable to determine what operation to perform.
    action = os.getenv("ACTION", "hint")

    # Retrieve the 'MOVES' environment variable to define the moves for this turn.
    moves = os.getenv("MOVES", [])

    # Retrieve the 'RESIGN' environment variable to define the moves for this turn.
    resign = os.getenv("RESIGN", "")

    # Retrieve the 'RESIGN' environment variable to define the moves for this turn.
    gnubg_id = os.getenv("GAME_ID", "AEAAAAAAAgAAAA:cAluAAAAAAAA")

    output_dir = os.environ.get("GNUBG_OUTPUT_DIR")
    
    if not output_dir:
        raise RuntimeError("GNUBG_OUTPUT_DIR not set")
    
    # Based on the action specified, execute the corresponding function.
    # This switches between creating a match, updating a match, or requesting a hint.
    if action in [
        "accept",
        "create",
        "error",
        "double",
        "drop",
        "move",
        "new",
        "reject",
        "resign",
        "roll",
        "take",
        "hint",
        "play",
    ]:
        # Call the function to update a match specified by the match reference.
        update_match(match_ref, gnubg_id, jacoby, variation, action, output_dir)
    else:
        # Log an error message if an unsupported action is specified.
        logger.error(f"Unsupported action '{action}'")


if __name__ == "__main__":
    main()