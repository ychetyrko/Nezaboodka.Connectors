/*======================================

		Nezaboodka Admin database
			field procedures

======================================*/

USE `nz_test_closure`;
﻿

DELIMITER //
DROP PROCEDURE IF EXISTS before_alter_fields //
CREATE PROCEDURE before_alter_fields()
BEGIN
	DROP TABLE IF EXISTS `nz_test_closure`.`field_add_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_add_list`(
		`owner_type_name` VARCHAR(128) NOT NULL CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL CHECK(`name` != ''),
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI` CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL CHECK(`type_name` != ''),
		`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
		`compare_options` ENUM
			(
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
		`back_ref_name` VARCHAR(128) DEFAULT NULL CHECK(`back_ref_name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`owner_type_name`, `name`)
	) ENGINE=`MEMORY` DEFAULT CHARSET=`UTF8` COLLATE `UTF8_GENERAL_CI`;

	DROP TABLE IF EXISTS `nz_test_closure`.`field_rem_list`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`field_rem_list`(
		`owner_type_name` VARCHAR(128) NOT NULL CHECK(`owner_type_name` != ''),
		`name` VARCHAR(128) NOT NULL CHECK(`name` != ''),

		CONSTRAINT `uc_pair`
			UNIQUE (`owner_type_name`, `name`)
	);
END //

/*---------------------------------------/
			Add fields
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS add_fields //
CREATE PROCEDURE add_fields()
BEGIN
	INSERT INTO `nz_test_closure`.`field`
	(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`, `owner_type_id`, `ref_type_id`)
	SELECT newf.`name`, newf.`col_name`, newf.`owner_type_name`, newf.`type_name`, newf.`is_list`, newf.`compare_options`, newf.`back_ref_name`, ownt.`id`, reft.`id`
	FROM `nz_test_closure`.`field_add_list` AS newf
	JOIN `nz_test_closure`.`type` AS ownt
	ON ownt.`name` = newf.`owner_type_name`
	LEFT JOIN `nz_test_closure`.`type` AS reft
	ON reft.`name` = newf.`type_name`;

	UPDATE `nz_test_closure`.`field` AS f1
	JOIN `nz_test_closure`.`field` AS f2
	ON f2.`name` = f1.`back_ref_name`
	SET f1.`back_ref_id` = f2.`id`;

	UPDATE `nz_test_closure`.`field` AS f1
	JOIN `nz_test_closure`.`field` AS f2
	ON f2.`id` = f1.`back_ref_id`
	SET f2.`back_ref_id` = f1.`id`,
		f2.`back_ref_name` = f1.`name`;

	DROP TABLE IF EXISTS `nz_test_closure`.`new_field`;
	CREATE TEMPORARY TABLE IF NOT EXISTS `nz_test_closure`.`new_field`(
		`id` INT NOT NULL,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`field`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;
	
	INSERT INTO `nz_test_closure`.`new_field`
	SELECT f.`id`
	FROM `nz_test_closure`.`field` AS f
	JOIN `nz_test_closure`.`field_add_list` AS newf
	ON f.`name` = newf.`name`
		AND f.`owner_type_name` = newf.`owner_type_name`;

	CALL _update_types_add_fields();
	DROP TABLE `nz_test_closure`.`new_field`;

	TRUNCATE TABLE `nz_test_closure`.`field_add_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_add_fields //
CREATE PROCEDURE _update_types_add_fields()
BEGIN
	DECLARE db_name VARCHAR(64) DEFAULT 'nz_test_closure';

	DECLARE t_type_id VARCHAR(128) DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE fields_defs TEXT;
	DECLARE fields_constraints TEXT;

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_test_closure`.`type` AS t;
	DECLARE CONTINUE HANDLER FOR NOT FOUND
		SET types_done = TRUE;
	
	OPEN type_cur;

	FETCH type_cur	
	INTO t_type_id, t_table_name;
	WHILE NOT types_done DO
		CALL _get_type_new_fields_and_constraints(t_type_id, FALSE, fields_defs, fields_constraints);

		IF (LENGTH(fields_defs) > 0) THEN
			IF (LENGTH(fields_constraints) > 0) THEN 
				SET fields_constraints = CONCAT(',', fields_constraints);
			END IF;

			SET @prep_str = CONCAT('
				ALTER TABLE `', db_name ,'`.`', t_table_name, '`
					ADD COLUMN (', fields_defs ,')
					', fields_constraints, ';
				');
/*
-- Debug
			SELECT @prep_str AS 'Altering query';
*/
			PREPARE p_alter_table FROM @prep_str;
			EXECUTE p_alter_table;
			DEALLOCATE PREPARE p_alter_table;
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
DROP PROCEDURE IF EXISTS remove_fields //
CREATE PROCEDURE remove_fields()
BEGIN
	DECLARE rem_fields_count INT DEFAULT 0;

	DROP TABLE IF EXISTS `nz_test_closure`.`removing_fields_list`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`removing_fields_list`(
		`id` INT NOT NULL UNIQUE,
		FOREIGN KEY (`id`)
			REFERENCES `nz_test_closure`.`field`(`id`)
			ON DELETE CASCADE
	) ENGINE=`MEMORY`;
	
	SELECT COUNT(`name`)
	INTO rem_fields_count
	FROM `nz_test_closure`.`field_rem_list`;

	INSERT INTO `nz_test_closure`.`removing_fields_list`
	SELECT f.`id`
	FROM `nz_test_closure`.`field` as f
	JOIN `nz_test_closure`.`field_rem_list` as remf
	ON f.`owner_type_name` = remf.`owner_type_name`
		AND f.`name` = remf.`name`;

	IF (ROW_COUNT() != rem_fields_count) THEN
		SIGNAL SQLSTATE '40000'
			SET MESSAGE_TEXT = "Not all fields can be deleted";
	END IF;

	CALL _update_types_remove_fields();

	UPDATE `nz_test_closure`.`field`
	SET `back_ref_name` = NULL
	WHERE `back_ref_id` IN (
		SELECT *
		FROM `nz_test_closure`.`removing_fields_list`
	);

	DELETE FROM `nz_test_closure`.`field`
	WHERE `id` IN (
		SELECT `id`
		FROM `nz_test_closure`.`removing_fields_list`
	);
	
	DROP TABLE `nz_test_closure`.`removing_fields_list`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_types_remove_fields //
CREATE PROCEDURE _update_types_remove_fields()
BEGIN
	DECLARE db_name VARCHAR(64) DEFAULT 'nz_test_closure';

	DECLARE c_type_id INT DEFAULT NULL;
	DECLARE t_table_name VARCHAR(64) DEFAULT NULL;
	DECLARE drop_columns TEXT DEFAULT '';
	DECLARE drop_constraints TEXT DEFAULT '';

	DECLARE types_done BOOLEAN DEFAULT FALSE;
	DECLARE type_cur CURSOR FOR
		SELECT t.`id` ,t.`table_name`
		FROM `nz_test_closure`.`type` AS t;
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
		CALL _get_type_removed_fields_and_constraints(c_type_id, drop_constraints, drop_columns);

		IF (LENGTH(drop_columns) > 0) THEN
			IF (LENGTH(drop_constraints) > 0) THEN 
				SET drop_constraints = CONCAT(drop_constraints, ',');
			END IF;
			SET @prep_str = CONCAT('
				ALTER TABLE `', db_name ,'`.`', t_table_name, '`
					', drop_constraints,'
					', drop_columns,'
				');
/*
-- Debug
			SELECT @prep_str AS 'Altering query';
*/
			PREPARE p_alter_table FROM @prep_str;
			EXECUTE p_alter_table;
			DEALLOCATE PREPARE p_alter_table;
		END IF;

		FETCH type_cur
		INTO c_type_id, t_table_name;
	END WHILE;

	CLOSE type_cur;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_removed_fields_and_constraints //
CREATE PROCEDURE _get_type_removed_fields_and_constraints
(IN cf_owner_type_id INT, OUT drop_constraints TEXT, OUT drop_columns TEXT)
BEGIN
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_id INT DEFAULT NULL;

	DECLARE done BOOLEAN DEFAULT FALSE;
	DECLARE fields_cur CURSOR FOR
		SELECT f.`col_name`, f.`id`, f.`ref_type_id`
		FROM `nz_test_closure`.`removing_fields_list` AS remf
		JOIN `nz_test_closure`.`field` AS f
		ON remf.`id` = f.`id`
		WHERE f.`owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `nz_test_closure`.`type_closure` AS clos
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
