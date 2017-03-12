DELIMITER //
DROP PROCEDURE IF EXISTS QEXEC //
CREATE PROCEDURE QEXEC(
	IN query_text TEXT
)
BEGIN
	DECLARE is_prepared BOOLEAN DEFAULT FALSE;
	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SET @prep_str = NULL;
		IF is_prepared THEN
			DEALLOCATE PREPARE p_prep_proc;
		END IF;
		RESIGNAL;
	END;

	SET @qexec_row_count = 0;
	SET @prep_str = query_text;

	PREPARE p_prep_proc FROM @prep_str;
	SET is_prepared = TRUE;

	EXECUTE p_prep_proc;
	SET @qexec_row_count = ROW_COUNT();

	DEALLOCATE PREPARE p_prep_proc;
	SET @prep_str = NULL;
END //
