/*======================================

		Nezaboodka Admin database
			public procedures

----------------------------------------

	Protocol to alter database schema:

 1. Call `before_alter_db_schema`
 2. Fill following tables:
	- `fields_add_list`
	- `fields_rem_list`
	- `types_add_list`
	- `types_rem_list`
 3. Call `alter_db_schema`

======================================*/﻿

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_db_schema //
CREATE PROCEDURE before_alter_db_schema()
BEGIN
	CALL before_alter_fields();
	CALL before_alter_types();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS alter_db_schema //
CREATE PROCEDURE alter_db_schema()
BEGIN
	-- To move all queries that provide implicit commit to the end of execution
	DROP TABLE IF EXISTS `nz_test_closure`.`alter_query`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`alter_query`(
		`ord` INT NOT NULL UNIQUE AUTO_INCREMENT,
		`query_text` TEXT DEFAULT NULL
	) ENGINE=`INNODB`;

	CALL remove_fields();
	CALL remove_types();

	CALL add_types();
	CALL add_fields();
/*
-- Debug
	SELECT * FROM `nz_test_closure`.`alter_query`;
*/
-- Apply all changes
	BEGIN
		DECLARE q_text TEXT DEFAULT NULL;
		
		DECLARE done BOOLEAN DEFAULT FALSE;
		DECLARE query_cur CURSOR FOR
			SELECT `query_text`
			FROM `nz_test_closure`.`alter_query`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET done = TRUE;

		OPEN query_cur;

		FETCH query_cur
		INTO q_text;
		WHILE NOT done DO
			SET @prep_str = q_text;
/*
-- Debug
			SELECT @prep_str;
*/
			PREPARE p_alter_query FROM @prep_str;
			EXECUTE p_alter_query;
			DEALLOCATE PREPARE p_alter_query;

			FETCH query_cur
			INTO q_text;
		END WHILE;

		CLOSE query_cur;
		DROP TABLE `nz_test_closure`.`alter_query`;
	END;

END //
