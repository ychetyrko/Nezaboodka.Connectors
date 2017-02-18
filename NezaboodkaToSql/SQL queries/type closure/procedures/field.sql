/*======================================

		Nezaboodka Admin database
			field procedures

======================================*/

USE `nz_test_closure`;
﻿

DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_fields //
CREATE PROCEDURE _before_alter_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`field_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_add_list`(
		`owner_type_name` VARCHAR(128) NOT NULL
			CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL
			CHECK(`name` != ''),
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
			CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL
			CHECK(`type_name` != ''),
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM (
			'None',
			'IgnoreCase',
			'IgnoreNonSpace',
			'IgnoreSymbols',
			'IgnoreKanaType',
			'IgnoreWidth',
			'OrdinalIgnoreCase',
			'StringSort',
			'Ordinal'
		) NOT NULL DEFAULT 'None',
		`back_ref_name` VARCHAR(128) DEFAULT NULL
			CHECK(`back_ref_name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`owner_type_name`, `name`)
	) ENGINE=`MEMORY`;

	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`field_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_rem_list`(
		`owner_type_name` VARCHAR(128) NOT NULL
			CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL
			CHECK(`name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`owner_type_name`, `name`)
	);
END //

/*---------------------------------------/
			Add fields
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_add_fields //
CREATE PROCEDURE _temp_before_add_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`new_field`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`new_field`(
		`id` INT NOT NULL
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_add_fields //
CREATE PROCEDURE _temp_after_add_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`new_field`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _add_fields //
CREATE PROCEDURE _add_fields()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
-- RESIGNAL;
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some fields can't be added";
	END;

	CALL QEXEC(CONCAT(
		"INSERT INTO `", @db_name, "`.`field`
		(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`, `owner_type_id`, `ref_type_id`)
		SELECT newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_list`, newf.`compare_options`, newf.`back_ref_name`, ownt.`id`, reft.`id`
		FROM `nz_test_closure`.`field_add_list` AS newf
		JOIN `", @db_name, "`.`type` AS ownt
		ON ownt.`name` = newf.`owner_type_name`
		LEFT JOIN `", @db_name, "`.`type` AS reft
		ON reft.`name` = newf.`type_name`;"
	));
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`name` = f1.`back_ref_name`
		SET f1.`back_ref_id` = f2.`id`;"
	));
	CALL QEXEC(CONCAT(
		"UPDATE `", @db_name, "`.`field` AS f1
		JOIN `", @db_name, "`.`field` AS f2
		ON f2.`id` = f1.`back_ref_id`
		SET f2.`back_ref_id` = f1.`id`,
			f2.`back_ref_name` = f1.`name`;"
	));
	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_test_closure`.`new_field`
		SELECT f.`id`
		FROM `", @db_name, "`.`field` AS f
		JOIN `nz_test_closure`.`field_add_list` AS newf
		ON f.`name` = newf.`name`
			AND f.`owner_type_name` = newf.`owner_type_name`;"
	));

	CALL _init_type_shadow(@db_name);
	CALL _update_types_add_fields();

	DELETE FROM `nz_test_closure`.`field_add_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_add_fields //
CREATE PROCEDURE _update_types_add_fields()
BEGIN
	DECLARE t_type_id VARCHAR(128) DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_test_closure`.`type_shadow` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO
		CALL _get_type_new_fields_and_constraints(
			t_type_id, FALSE, fields_defs, fields_constraints
		);

		IF (LENGTH(fields_defs) > 0) THEN
			IF (LENGTH(fields_constraints) > 0) THEN 
				SET fields_constraints = CONCAT(',', fields_constraints);
			END IF;

			SET @prep_str = CONCAT('
				ALTER TABLE `', @db_name, '`.`', t_table_name, '`
					ADD COLUMN (', fields_defs ,')
					', fields_constraints, ';
				');
/*
-- Debug
			SELECT @prep_str AS 'Altering query';
*/
			-- check if types are valid
			PREPARE p_alter_table FROM @prep_str;
			DEALLOCATE PREPARE p_alter_table;

			INSERT INTO `nz_test_closure`.`alter_query`
			(`query_text`)
			VALUE
			(@prep_str);
		END IF;

		FETCH type_cur
		INTO t_type_id, t_table_name;
	END WHILE;

	CLOSE type_cur;
END //

/*---------------------------------------/
			Remove fields
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_remove_fields //
CREATE PROCEDURE _temp_before_remove_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`removing_fields_list`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`removing_fields_list`(
		`id` INT NOT NULL UNIQUE
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_remove_fields //
CREATE PROCEDURE _temp_after_remove_fields()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`removing_fields_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_fields //
CREATE PROCEDURE _remove_fields()
BEGIN
	DECLARE rem_fields_count INT DEFAULT 0;
	DECLARE real_rem_fields_count INT DEFAULT 0;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
-- RESIGNAL;
		SIGNAL SQLSTATE 'HY000'
			SET MESSAGE_TEXT = "Some fields can't be removed";
	END;

	SELECT COUNT(`name`)
	INTO rem_fields_count
	FROM `nz_test_closure`.`field_rem_list`;

	CALL QEXEC(CONCAT(
		"INSERT INTO `nz_test_closure`.`removing_fields_list`
		SELECT f.`id`
		FROM `", @db_name, "`.`field` AS f
		JOIN `nz_test_closure`.`field_rem_list` AS remf
		ON f.`owner_type_name` = remf.`owner_type_name`
			AND f.`name` = remf.`name`;"
	));
/*
-- Debug
	CALL QEXEC(CONCAT(
		"SELECT f.`name`, f.`owner_type_name`
		FROM `", @db_name, "`.`field` AS f
		JOIN `nz_test_closure`.`field_rem_list` AS remf
		ON f.`owner_type_name` = remf.`owner_type_name`
			AND f.`name` = remf.`name`;"
	));
*/
	SELECT COUNT(`id`)
	INTO real_rem_fields_count
	FROM `nz_test_closure`.`removing_fields_list`;

	IF (real_rem_fields_count != rem_fields_count) THEN
		SIGNAL SQLSTATE 'HY000';
	END IF;
/*
-- Debug
	SELECT '1st';
	CALL QEXEC(CONCAT(
		"SELECT * FROM `", @db_name, "`.`type`;"
	));
*/
	CALL _init_type_shadow(@db_name);
/*
-- Debug
	SELECT '2nd';
	SELECT * FROM `nz_test_closure`.`type_shadow`;
*/
	CALL _init_type_closure_shadow(@db_name);
/*
-- Debug
	SELECT '3rd';
	SELECT * FROM `nz_test_closure`.`type_shadow`;
*/
	CALL _init_field_shadow(@db_name);
/*
-- Debug
	SELECT '4th';
	SELECT * FROM `nz_test_closure`.`type_shadow`;
*/

	CALL _update_types_remove_fields();

	CALL _remove_deleted_fields_from_table();
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_remove_fields //
CREATE PROCEDURE _update_types_remove_fields()
BEGIN
	DECLARE c_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE drop_columns TEXT DEFAULT '';
	DECLARE drop_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_test_closure`.`type_shadow` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN type_cur;

	FETCH type_cur	
	INTO c_type_id, t_table_name;
	WHILE NOT types_done DO
/*
-- Debug
		SELECT c_type_id AS 'Current type id';
*/
		CALL _get_type_removed_fields_and_constraints(
			c_type_id, drop_constraints, drop_columns
		);

		IF (LENGTH(drop_columns) > 0) THEN
			IF (LENGTH(drop_constraints) > 0) THEN 
				SET drop_constraints = CONCAT(drop_constraints, ',');
			END IF;
			SET @prep_str = CONCAT('
				ALTER TABLE `', @db_name, '`.`', t_table_name, '`
					', drop_constraints,'
					', drop_columns,'
				');
/*
-- Debug
			SELECT @prep_str AS 'Altering query';
*/
			INSERT INTO `nz_test_closure`.`alter_query`
			(`query_text`)
			VALUE
			(@prep_str);
		END IF;

		FETCH type_cur
		INTO c_type_id, t_table_name;
	END WHILE;

	CLOSE type_cur;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_removed_fields_and_constraints //
CREATE PROCEDURE _get_type_removed_fields_and_constraints(
	IN cf_owner_type_id INT,
	OUT drop_constraints TEXT,
	OUT drop_columns TEXT
)
BEGIN
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_id INT DEFAULT NULL;

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE fields_cur CURSOR FOR
		SELECT f.`col_name`, f.`id`, f.`ref_type_id`
		FROM `nz_test_closure`.`removing_fields_list` AS remf
		JOIN `nz_test_closure`.`field_shadow` AS f
		ON remf.`id` = f.`id`
		WHERE f.`owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `nz_test_closure`.`type_closure_shadow` AS clos
			WHERE clos.`descendant` = cf_owner_type_id
		);
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET done = TRUE;

	SET drop_constraints = '';
	SET drop_columns = '';

	OPEN fields_cur;

	FETCH fields_cur
	INTO cf_col_name, cf_id, cf_ref_type_id;
	WHILE NOT done DO
		SET drop_columns = CONCAT(drop_columns, ',
			DROP COLUMN ', cf_col_name
		);
		
		IF !(cf_ref_type_id IS NULL) THEN
			SET drop_constraints = CONCAT(drop_constraints, ',
			DROP FOREIGN KEY FK_', cf_owner_type_id, '_', cf_id);
		END IF;
		
		FETCH fields_cur
		INTO cf_col_name, cf_id, cf_ref_type_id;
	END WHILE;
	
	IF (LEFT(drop_columns, 1) = ',') THEN
		SET drop_columns = SUBSTRING(drop_columns, 2);
	END IF;
	
	IF (LEFT(drop_constraints, 1) = ',') THEN
		SET drop_constraints = SUBSTRING(drop_constraints, 2);
	END IF;
END //
